/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

namespace SmartHopper.Infrastructure.Tests.AICall.Logging
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using SmartHopper.Infrastructure.AICall.Logging;
    using SmartHopper.ProviderSdk.Logging;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="SmartHopperLogFileSink"/>.
    /// </summary>
    [Collection("InfrastructureLogging")]
#pragma warning disable CA1031, CA1307, SA1204 // Test cleanup and xUnit string assertions.
    public class SmartHopperLogFileSinkTests
    {
        /// <summary>
        /// The sink should write a formatted line to the current log file after flushing.
        /// </summary>
        [Fact]
        public void WriteAndFlush_WritesFormattedLineToFile()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var expectedPath = Path.Combine(baseDirectory, $"smart-hopper-{DateTime.UtcNow:yyyyMMdd}.log");

            using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory))
            {
                var entry = new SmartHopperLogEntry(
                    new DateTimeOffset(2026, 9, 3, 14, 32, 0, TimeSpan.Zero),
                    ProviderLogLevel.Info,
                    "Test",
                    "hello from file sink",
                    null);

                sink.Write(entry);
                sink.Flush();
            }

            try
            {
                Assert.True(File.Exists(expectedPath), $"Log file was not created at {expectedPath}");
                var content = File.ReadAllText(expectedPath);
                Assert.Contains("[Info]", content);
                Assert.Contains("[Test]", content);
                Assert.Contains("hello from file sink", content);
            }
            finally
            {
                Cleanup(baseDirectory);
            }
        }

        /// <summary>
        /// The sink should flush any queued lines when it is disposed.
        /// </summary>
        [Fact]
        public void Dispose_FlushesPendingLines()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var expectedPath = Path.Combine(baseDirectory, $"smart-hopper-{DateTime.UtcNow:yyyyMMdd}.log");

            using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory))
            {
                var entry = new SmartHopperLogEntry(
                    DateTimeOffset.UtcNow,
                    ProviderLogLevel.Warning,
                    "DisposeTest",
                    "flush on dispose",
                    null);

                sink.Write(entry);
            }

            try
            {
                Assert.True(File.Exists(expectedPath), $"Log file was not created at {expectedPath}");
                var content = File.ReadAllText(expectedPath);
                Assert.Contains("[Warning]", content);
                Assert.Contains("[DisposeTest]", content);
                Assert.Contains("flush on dispose", content);
            }
            finally
            {
                Cleanup(baseDirectory);
            }
        }

        /// <summary>
        /// The sink should delete log files older than the configured retention period.
        /// </summary>
        [Fact]
        public void CleanupOldFiles_RemovesExpiredFiles()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var oldFile = Path.Combine(baseDirectory, "smart-hopper-old.log");

            try
            {
                File.WriteAllText(oldFile, "expired");
                File.SetCreationTimeUtc(oldFile, DateTime.UtcNow.AddDays(-5));

                using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory))
                {
                    sink.CleanupOldFiles();
                }

                Assert.False(File.Exists(oldFile), "Expired log file was not deleted.");
            }
            finally
            {
                Cleanup(baseDirectory);
            }
        }

        /// <summary>
        /// The sink should keep log files newer than the configured retention period.
        /// </summary>
        [Fact]
        public void CleanupOldFiles_KeepsRecentFiles()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var today = DateTime.UtcNow.Date;
            var recentFile = Path.Combine(baseDirectory, $"smart-hopper-{today:yyyyMMdd}.log");

            using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory))
            {
                // Write one line to create the current file.
                sink.Write(new SmartHopperLogEntry(
                    DateTimeOffset.UtcNow,
                    ProviderLogLevel.Info,
                    "Recent",
                    "recent",
                    null));
                sink.Flush();
            }

            try
            {
                Assert.True(File.Exists(recentFile), "Recent log file should not be deleted.");
            }
            finally
            {
                Cleanup(baseDirectory);
            }
        }

        /// <summary>
        /// When the current file is from a previous day, a flush should roll to today's file.
        /// </summary>
        [Fact]
        public void Write_OnDateBoundary_CreatesNewFile()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var yesterdayPath = Path.Combine(baseDirectory, $"smart-hopper-{yesterday:yyyyMMdd}.log");
            var todayPath = Path.Combine(baseDirectory, $"smart-hopper-{today:yyyyMMdd}.log");

            using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory))
            {
                // Prepare the sink as if it has been writing to yesterday's file.
                SetField(sink, "_currentFileDate", yesterday);
                SetField(sink, "_currentFilePath", yesterdayPath);

                var entry = new SmartHopperLogEntry(
                    DateTimeOffset.UtcNow,
                    ProviderLogLevel.Info,
                    "DateRoll",
                    "rolled to today",
                    null);

                sink.Write(entry);
                sink.Flush();
                sink.Dispose();

                Assert.True(File.Exists(todayPath), "Today's log file was not created.");
                var content = File.ReadAllText(todayPath);
                Assert.Contains("[DateRoll]", content);
                Assert.Contains("rolled to today", content);
            }

            Cleanup(baseDirectory);
        }

        /// <summary>
        /// When the current file exceeds the configured size limit, a flush should roll to an indexed file.
        /// </summary>
        [Fact]
        public void Write_ExceedsSizeRollsToIndexedFile()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var today = DateTime.UtcNow.Date;
            var todayPath = Path.Combine(baseDirectory, $"smart-hopper-{today:yyyyMMdd}.log");
            var rolledPath = Path.Combine(baseDirectory, $"smart-hopper-{today:yyyyMMdd}-1.log");

            using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory, maxFileSizeBytes: 64))
            {
                // First line is large enough to trigger a roll after flush.
                var entry = new SmartHopperLogEntry(
                    DateTimeOffset.UtcNow,
                    ProviderLogLevel.Info,
                    "SizeRoll",
                    new string('x', 256),
                    null);

                sink.Write(entry);
                sink.Flush();

                // A second line should go to the rolled file.
                var second = new SmartHopperLogEntry(
                    DateTimeOffset.UtcNow,
                    ProviderLogLevel.Info,
                    "SizeRoll",
                    "second line",
                    null);

                sink.Write(second);
                sink.Flush();
                sink.Dispose();

                Assert.True(File.Exists(rolledPath), "Rolled log file was not created.");
                Assert.Contains("second line", File.ReadAllText(rolledPath));
                Assert.Contains(new string('x', 256), File.ReadAllText(todayPath));
            }

            Cleanup(baseDirectory);
        }

        /// <summary>
        /// A scope should be included in the formatted line.
        /// </summary>
        [Fact]
        public void Write_EntryWithScope_IncludesScopeInFile()
        {
            var baseDirectory = CreateUniqueTestDirectory();
            var expectedPath = Path.Combine(baseDirectory, $"smart-hopper-{DateTime.UtcNow:yyyyMMdd}.log");

            using (var sink = new SmartHopperLogFileSink(new SmartHopperLogFormatter(), baseDirectory))
            {
                var scope = new Dictionary<string, object>
                {
                    { "RequestId", "123" },
                };

                var entry = new SmartHopperLogEntry(
                    DateTimeOffset.UtcNow,
                    ProviderLogLevel.Info,
                    "ScopeTest",
                    "with scope",
                    scope);

                sink.Write(entry);
                sink.Flush();
            }

            try
            {
                var content = File.ReadAllText(expectedPath);
                Assert.Contains("RequestId=123", content);
            }
            finally
            {
                Cleanup(baseDirectory);
            }
        }

        private static string CreateUniqueTestDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"sh-logtests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Cleanup(string baseDirectory)
        {
            try
            {
                if (Directory.Exists(baseDirectory))
                {
                    Directory.Delete(baseDirectory, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static void SetField(object target, string fieldName, object? value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field!.SetValue(target, value);
        }
    }
#pragma warning restore CA1031, CA1307, SA1204
}
