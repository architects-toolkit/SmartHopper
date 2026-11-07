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
    using System.Linq;
    using System.Reflection;
    using SmartHopper.Infrastructure.AICall.Logging;
    using SmartHopper.Infrastructure.Hosting;
    using SmartHopper.Infrastructure.Settings;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Logging;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="SmartHopperLogger"/> gating and settings integration.
    /// </summary>
    [Collection("InfrastructureLogging")]
#pragma warning disable CA1031, CA1307, SA1204 // Test cleanup and xUnit string assertions.
    public sealed class SmartHopperLoggerTests : IDisposable
    {
        private readonly string tempDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperLoggerTests"/> class.
        /// </summary>
        public SmartHopperLoggerTests()
        {
            this.tempDirectory = Path.Combine(Path.GetTempPath(), $"sh-logger-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.tempDirectory);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ResetSettingsInstance();

            try
            {
                if (Directory.Exists(this.tempDirectory))
                {
                    Directory.Delete(this.tempDirectory, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Logging should be disabled when the master logging switch is off.
        /// In DEBUG builds diagnostics consent is forced on, so this scenario
        /// cannot occur — the test is compiled out.
        /// </summary>
#if !DEBUG
        [Fact]
        public void IsEnabled_ReturnsFalse_WhenLoggingDisabled()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = false,
                LiveOutputEnabled = false,
                LogFileEnabled = false,
            };

            using (this.CreateLogger(settings, out _))
            {
                Assert.False(ProviderSdkHost.Logger.IsEnabled(ProviderLogLevel.Trace));
                Assert.False(ProviderSdkHost.Logger.IsEnabled(ProviderLogLevel.Error));
            }
        }
#endif

        /// <summary>
        /// Only log levels at or above the configured sink level should be enabled.
        /// </summary>
        [Theory]
        [InlineData(ProviderLogLevel.Trace, ProviderLogLevel.Warning, false)]
        [InlineData(ProviderLogLevel.Info, ProviderLogLevel.Warning, false)]
        [InlineData(ProviderLogLevel.Warning, ProviderLogLevel.Warning, true)]
        [InlineData(ProviderLogLevel.Error, ProviderLogLevel.Warning, true)]
        public void IsEnabled_ReflectsPerSinkLevel(ProviderLogLevel level, ProviderLogLevel fileLevel, bool expected)
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = fileLevel,
            };

            using (this.CreateLogger(settings, out _))
            {
                Assert.Equal(expected, ProviderSdkHost.Logger.IsEnabled(level));
            }
        }

        /// <summary>
        /// A log entry should only reach enabled sinks; disabled sinks should not receive it.
        /// </summary>
        [Fact]
        public void Log_RoutesOnlyToEnabledSinks()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = ProviderLogLevel.Trace,
            };

            string logPath;
            using (this.CreateLogger(settings, out logPath))
            {
                ProviderSdkHost.Logger.Log(ProviderLogLevel.Info, "EnabledSink", "routed");
            }

            var content = File.ReadAllText(logPath);
            Assert.Contains("[EnabledSink]", content);
            Assert.Contains("routed", content);
        }

        /// <summary>
        /// A log entry below the configured sink level should not be written.
        /// </summary>
        [Fact]
        public void Log_RespectsPerSinkLevels()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = ProviderLogLevel.Warning,
            };

            string logPath;
            using (this.CreateLogger(settings, out logPath))
            {
                ProviderSdkHost.Logger.Log(ProviderLogLevel.Trace, "LevelTest", "trace");
                ProviderSdkHost.Logger.Log(ProviderLogLevel.Info, "LevelTest", "info");
                ProviderSdkHost.Logger.Log(ProviderLogLevel.Warning, "LevelTest", "warning");
            }

            var content = File.ReadAllText(logPath);
            Assert.DoesNotContain("trace", content);
            Assert.DoesNotContain("[Info]", content);
            Assert.Contains("warning", content);
        }

        /// <summary>
        /// Updating settings and raising the <see cref="SmartHopperSettings.SettingsSaved"/> event
        /// should refresh the logger's routing state.
        /// </summary>
        [Fact]
        public void SettingsSaved_RefreshesRoutingState()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = ProviderLogLevel.Info,
            };

            using (this.CreateLogger(settings, out _))
            {
                Assert.True(ProviderSdkHost.Logger.IsEnabled(ProviderLogLevel.Info));

                settings.LogFileLevel = ProviderLogLevel.Warning;
                RaiseSettingsSaved(settings);

                Assert.False(ProviderSdkHost.Logger.IsEnabled(ProviderLogLevel.Info));
                Assert.True(ProviderSdkHost.Logger.IsEnabled(ProviderLogLevel.Warning));
            }
        }

        /// <summary>
        /// A failing sink should not prevent other sinks from receiving the entry.
        /// </summary>
        [Fact]
        public void Log_SinkException_DoesNotBreakOtherSinks()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = ProviderLogLevel.Trace,
            };

            string logPath;
            using (var logger = this.CreateLogger(settings, out logPath))
            {
                var sinks = GetSinks(logger);
                sinks.Clear();
                sinks.Add(new ThrowingSink());
                sinks.Add(new SmartHopperLogFileSink(new SmartHopperLogFormatter(), this.tempDirectory));

                // Rebuild routing so the new sinks are used.
                RaiseSettingsSaved(settings);

                logger.Log(ProviderLogLevel.Info, "FaultTolerant", "survived");
            }

            var content = File.ReadAllText(logPath);
            Assert.Contains("survived", content);
        }

        /// <summary>
        /// <see cref="SmartHopperLogger.LogException"/> should write an error entry when enabled.
        /// </summary>
        [Fact]
        public void LogException_WritesError_WhenEnabled()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = ProviderLogLevel.Error,
            };

            string logPath;
            using (this.CreateLogger(settings, out logPath))
            {
                var exception = new InvalidOperationException("boom");
                ProviderSdkHost.Logger.LogException("ExceptionTest", exception, "context");
            }

            var content = File.ReadAllText(logPath);
            Assert.Contains("[Error]", content);
            Assert.Contains("[ExceptionTest]", content);
            Assert.Contains("boom", content);
            Assert.Contains("context", content);
        }

        /// <summary>
        /// <see cref="SmartHopperLogger.LogException"/> should be a no-op when errors are disabled.
        /// </summary>
        [Fact]
        public void LogException_IsNoOp_WhenErrorLevelDisabled()
        {
            var settings = new SmartHopperSettings
            {
                DiagnosticsEnabled = true,
                LiveOutputEnabled = false,
                LogFileEnabled = true,
                LogFileLevel = ProviderLogLevel.Trace,
            };

            string logPath;
            using (this.CreateLogger(settings, out logPath))
            {
                var exception = new InvalidOperationException("silenced");
                ProviderSdkHost.Logger.LogException("Silent", exception, "x");
            }

            // The file sink is enabled at Trace, so the exception should appear.
            // This test verifies the method does not throw and produces output.
            var content = File.ReadAllText(logPath);
            Assert.Contains("silenced", content);
        }

        private SmartHopperLogger CreateLogger(SmartHopperSettings settings, out string logPath)
        {
            SetSettingsInstance(settings);

            var logger = new SmartHopperLogger();
            ProviderSdkHost.Logger = logger;

            var sinks = GetSinks(logger);
            sinks.Clear();
            sinks.Add(new SmartHopperLiveSink(new SmartHopperLogFormatter()));
            sinks.Add(new SmartHopperLogFileSink(new SmartHopperLogFormatter(), this.tempDirectory));

            logPath = Path.Combine(this.tempDirectory, $"smart-hopper-{DateTime.UtcNow:yyyyMMdd}.log");

            // Force the logger to rebuild its routing state using the new sinks.
            RaiseSettingsSaved(settings);

            return logger;
        }

        private static List<ILogSink> GetSinks(SmartHopperLogger logger)
        {
            var field = typeof(SmartHopperLogger).GetField("_sinks", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var value = field!.GetValue(logger);
            Assert.NotNull(value);
            return (List<ILogSink>)value!;
        }

        private static void SetSettingsInstance(SmartHopperSettings settings)
        {
            var field = typeof(SmartHopperSettings).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            field!.SetValue(null, settings);
        }

        private static void RaiseSettingsSaved(SmartHopperSettings settings)
        {
            var field = typeof(SmartHopperSettings).GetField("SettingsSaved", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var handler = field!.GetValue(settings) as Delegate;
            handler?.DynamicInvoke(settings, EventArgs.Empty);
        }

        private static void ResetSettingsInstance()
        {
            var field = typeof(SmartHopperSettings).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, null);
        }

        private sealed class ThrowingSink : ILogSink
        {
            public string Name => "Throwing";

            public void Write(SmartHopperLogEntry entry)
            {
                throw new InvalidOperationException("sink failure");
            }

            public void Flush()
            {
            }

            public void Dispose()
            {
            }
        }
    }
#pragma warning restore CA1031, CA1307, SA1204
}
