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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using SmartHopper.Infrastructure.AICall.Logging;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Utils;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Infrastructure.Diagnostics
{
    /// <summary>
    /// Cross-platform helpers for collecting, redacting, and sharing SmartHopper diagnostics.
    /// </summary>
    public static class SmartHopperDiagnostics
    {
        /// <summary>
        /// HRESULT (<c>CLIPBRD_E_CANT_OPEN</c>) returned when the Windows clipboard is held open
        /// by another application (e.g. Remote Desktop's rdpclip or a clipboard manager).
        /// </summary>
        public const int ClipboardInUseErrorCode = unchecked((int)0x800401D0);

        private const int ClipboardMaxAttempts = 10;
        private const int ClipboardRetryDelayMs = 200;

        /// <summary>
        /// Opens the log folder in the platform file manager, creating it if necessary.
        /// </summary>
        public static void OpenLogFolder()
        {
            var logDirectory = SmartHopperLogFileSink.DefaultLogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            OpenInFileManager(logDirectory);
        }

        /// <summary>
        /// Generates a privacy-safe markdown diagnostics report for sharing on GitHub.
        /// </summary>
        /// <param name="maxLogBytes">Maximum number of bytes to include from the latest log file.</param>
        /// <returns>A redacted diagnostics report.</returns>
        public static string GenerateReport(int maxLogBytes = 50000)
        {
            var report = new StringBuilder();
            report.AppendLine("## SmartHopper Diagnostics");
            report.AppendLine();
            report.AppendLine(FormattableString.Invariant($"- Version: {VersionHelper.GetDisplayVersion()}"));
            report.AppendLine(FormattableString.Invariant($"- Platform: {VersionHelper.GetPlatform()}"));
            report.AppendLine(FormattableString.Invariant($"- Configuration: {GetBuildConfiguration()}"));
            report.AppendLine();

            var logPath = GetLatestLogFilePath();
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            {
                report.AppendLine("## Recent Logs");
                report.AppendLine();
                report.AppendLine("No log file found.");
                return report.ToString();
            }

            var logContent = ReadTail(logPath, maxLogBytes);
            var scrubbed = SmartHopperLogScrubber.Scrub(logContent);

            report.AppendLine("## Recent Logs");
            report.AppendLine();
            report.AppendLine(scrubbed);

            return report.ToString();
        }

        /// <summary>
        /// Generates a redacted diagnostic zip and returns the path to the archive.
        /// </summary>
        /// <param name="outputPath">Optional output path. If null, a timestamped zip is created on the desktop.</param>
        /// <returns>The full path of the generated zip file.</returns>
        public static string GenerateDiagnosticZip(string? outputPath = null)
        {
            var logPath = GetLatestLogFilePath();
            var actualOutputPath = outputPath ?? GetDefaultZipPath();

            var tempDirectory = Path.Combine(Path.GetTempPath(), $"sh-diagnostics-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            try
            {
                var infoPath = Path.Combine(tempDirectory, "info.txt");
                File.WriteAllText(infoPath, GenerateReport(maxLogBytes: 100000));

                // diagnostics.json carries the anonymous install hash and versions so a
                // shared zip can be correlated when the user reports an issue.
                var diagnosticsPath = Path.Combine(tempDirectory, "diagnostics.json");
                File.WriteAllText(diagnosticsPath, BuildDiagnosticsJson());

                if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                {
                    var logContent = File.ReadAllText(logPath);
                    var scrubbed = SmartHopperLogScrubber.Scrub(logContent);
                    var scrubbedLogPath = Path.Combine(tempDirectory, "log.txt");
                    File.WriteAllText(scrubbedLogPath, scrubbed);
                }

                if (File.Exists(actualOutputPath))
                {
                    File.Delete(actualOutputPath);
                }

                using (var stream = new FileStream(actualOutputPath, FileMode.Create, FileAccess.Write))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    AddFileToArchive(archive, infoPath, "info.txt");
                    AddFileToArchive(archive, diagnosticsPath, "diagnostics.json");

                    var scrubbedLogPath = Path.Combine(tempDirectory, "log.txt");
                    if (File.Exists(scrubbedLogPath))
                    {
                        AddFileToArchive(archive, scrubbedLogPath, "log.txt");
                    }
                }

                return actualOutputPath;
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// Copies the given text to the system clipboard in a cross-platform manner.
        /// On Windows the clipboard is a shared global resource, so a brief contention
        /// (e.g. Remote Desktop or a clipboard manager holding it open) is retried a
        /// few times before the failure is surfaced to the caller.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        public static void CopyToClipboard(string text)
        {
#if WINDOWS
            var data = new System.Windows.Forms.DataObject();
            data.SetText(text ?? string.Empty, System.Windows.Forms.TextDataFormat.UnicodeText);

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetDataObject(data, copy: true, retryTimes: 0, retryDelay: 0);
                    return;
                }
                catch (ExternalException ex) when (ex.ErrorCode == ClipboardInUseErrorCode && attempt < ClipboardMaxAttempts)
                {
                    System.Threading.Thread.Sleep(ClipboardRetryDelayMs);
                }
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "pbcopy";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardInput = true;
                    process.Start();
                    process.StandardInput.Write(text ?? string.Empty);
                    process.StandardInput.Close();
                    process.WaitForExit();
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Clipboard is not supported on this platform.");
            }
#endif
        }

        /// <summary>
        /// Gets the full path of the most recently written log file.
        /// </summary>
        /// <returns>The path, or <see cref="string.Empty"/> if no log file exists.</returns>
        private static string GetLatestLogFilePath()
        {
            var logDirectory = SmartHopperLogFileSink.DefaultLogDirectory;
            if (!Directory.Exists(logDirectory))
            {
                return string.Empty;
            }

            var files = Directory.GetFiles(logDirectory, "smart-hopper-*.log");
            if (files.Length == 0)
            {
                return string.Empty;
            }

            return files
                .Select(f => new { Path = f, Time = File.GetLastWriteTimeUtc(f) })
                .OrderByDescending(f => f.Time)
                .First()
                .Path;
        }

        /// <summary>
        /// Reads the trailing portion of a file up to <paramref name="maxBytes"/>.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="maxBytes">Maximum bytes to read.</param>
        /// <returns>The trailing content, scrubbed-ready.</returns>
        private static string ReadTail(string path, int maxBytes)
        {
            var info = new FileInfo(path);
            if (info.Length <= maxBytes)
            {
                return File.ReadAllText(path);
            }

            var start = info.Length - maxBytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Position = start;

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    // Discard the first (likely partial) line.
                    reader.ReadLine();
                    return reader.ReadToEnd() ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Adds a file from disk to the zip archive with the given entry name.
        /// </summary>
        private static string BuildDiagnosticsJson()
        {
            string installationHash = "unknown";
            try
            {
                installationHash = GetInstallationHash();
            }
            catch
            {
                // Hash must never break diagnostics export.
            }

            var json = new Newtonsoft.Json.Linq.JObject
            {
                ["installationHash"] = installationHash,
                ["smarthopperVersion"] = Utils.VersionHelper.GetDisplayVersion(),
                ["osType"] = Utils.VersionHelper.GetPlatform(),
            };
            return json.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Computes a local, anonymous installation hash used only inside user-generated
        /// diagnostics exports. Nothing leaves the machine unless the user shares the zip.
        /// </summary>
        private static string GetInstallationHash()
        {
            var salt = SmartHopperSettings.Instance.DiagnosticsInstallationSalt;
            var identity = $"{salt}|{Environment.MachineName}|{Environment.UserName}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }

        private static void AddFileToArchive(ZipArchive archive, string filePath, string entryName)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fileStream.CopyTo(entryStream);
            }
        }

        /// <summary>
        /// Gets a default output path on the desktop for the diagnostic zip.
        /// </summary>
        private static string GetDefaultZipPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(desktop, $"smart-hopper-diagnostics-{timestamp}.zip");
        }

        /// <summary>
        /// Returns the current build configuration.
        /// </summary>
        private static string GetBuildConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        /// <summary>
        /// Opens a directory in the platform file manager.
        /// </summary>
        private static void OpenInFileManager(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("open", path) { UseShellExecute = true });
            }
            else
            {
                throw new PlatformNotSupportedException("Opening the file manager is not supported on this platform.");
            }
        }

        /// <summary>
        /// Best-effort deletion of a directory.
        /// </summary>
        private static void TryDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup.
            }
            catch (SecurityException)
            {
                // Best-effort cleanup.
            }
        }
    }
}
