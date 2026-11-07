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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.Logging;



namespace SmartHopper.Infrastructure.AICall.Logging
{
    /// <summary>
    /// Rolling log file sink. Buffers formatted log lines in a background queue and flushes
    /// them on a timer or when the sink is disposed.
    /// </summary>
    public sealed class SmartHopperLogFileSink : ILogSink
    {
        private const long DefaultMaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB
        private const int MaxPendingLines = 50000;

        private readonly ILogFormatter _formatter;
        private readonly string _baseDirectory;
        private readonly string _fileNamePrefix;
        private readonly string _fileExtension;
        private readonly ConcurrentQueue<string> _queue;
        private readonly Timer _flushTimer;
        private readonly object _writerLock = new();

        private DateTime _currentFileDate;
        private int _currentFileIndex;
        private string _currentFilePath = string.Empty;
        private StreamWriter? _writer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperLogFileSink"/> class
        /// using the default log directory under the application data folder.
        /// </summary>
        /// <param name="formatter">The formatter to use for output.</param>
        public SmartHopperLogFileSink(ILogFormatter formatter)
            : this(formatter, DefaultBaseDirectory(), DefaultMaxFileSizeBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperLogFileSink"/> class.
        /// </summary>
        /// <param name="formatter">The formatter to use for output.</param>
        /// <param name="baseDirectory">The directory where log files are written.</param>
        /// <param name="maxFileSizeBytes">The size at which the current log file is rolled to an indexed file.</param>
        public SmartHopperLogFileSink(ILogFormatter formatter, string baseDirectory, long maxFileSizeBytes = DefaultMaxFileSizeBytes)
        {
            this._formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            this._baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            this._fileNamePrefix = "smart-hopper-";
            this._fileExtension = ".log";
            this.MaxFileSizeBytes = maxFileSizeBytes;
            this._queue = new ConcurrentQueue<string>();

            this.CleanupOldFiles();

            this._flushTimer = new Timer(
                _ => this.Flush(),
                null,
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(250));
        }

        /// <summary>
        /// Gets or sets the maximum size, in bytes, of the current log file before it is rolled.
        /// </summary>
        public long MaxFileSizeBytes { get; set; }

        /// <inheritdoc />
        public string Name => "File";

        /// <summary>
        /// Gets the directory where log files are written.
        /// </summary>
        public string LogsDirectory => this._baseDirectory;

        /// <summary>
        /// Gets the default log directory under the application data folder.
        /// </summary>
        public static string DefaultLogDirectory => DefaultBaseDirectory();

        private static string DefaultBaseDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Grasshopper", "SmartHopper", "Logs");
        }

        /// <inheritdoc />
        public void Write(SmartHopperLogEntry entry)
        {
            if (this._disposed)
            {
                return;
            }

            // Bound the pending queue like the OTLP publisher: a logging burst
            // must never exhaust host memory — oldest lines are dropped first.
            while (this._queue.Count >= MaxPendingLines)
            {
                this._queue.TryDequeue(out _);
            }

            this._queue.Enqueue(this._formatter.Format(entry));
        }

        /// <inheritdoc />
        public void Flush()
        {
            try
            {
                lock (this._writerLock)
                {
                    if (this._disposed)
                    {
                        return;
                    }

                    var lines = new List<string>();
                    while (this._queue.TryDequeue(out var line))
                    {
                        lines.Add(line);
                    }

                    if (lines.Count == 0)
                    {
                        return;
                    }

                    this.EnsureCurrentWriter();

                    if (this._writer == null)
                    {
                        return;
                    }

                    foreach (var line in lines)
                    {
                        this._writer.WriteLine(line);
                    }

                    this._writer.Flush();
                    this.RollIfNeeded();
                }
            }
            catch
            {
                // Flush also runs on a background timer: an I/O failure (deleted
                // directory, full disk, transient error) must never propagate
                // and take the host process down. Pending lines are dropped.
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this._flushTimer?.Dispose();

            lock (this._writerLock)
            {
                this.Flush();
                this._disposed = true;
                this._writer?.Flush();
                this._writer?.Dispose();
                this._writer = null;
            }
        }

        /// <summary>
        /// Cleans up log files older than the configured retention period.
        /// </summary>
        public void CleanupOldFiles()
        {
            try
            {
                if (!Directory.Exists(this._baseDirectory))
                {
                    return;
                }

                var retentionDays = this.GetSettings()?.LogRetentionDays ?? 3;
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

                foreach (var file in Directory.GetFiles(this._baseDirectory, this._fileNamePrefix + "*" + this._fileExtension))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTimeUtc < cutoff)
                        {
                            fileInfo.Delete();
                        }
                    }
                    catch
                    {
                        // Ignore per-file errors; continue cleanup.
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors to avoid breaking logging.
            }
        }

        private void EnsureCurrentWriter()
        {
            var today = DateTime.UtcNow.Date;

            if (this._writer != null && this._currentFileDate == today)
            {
                return;
            }

            this._writer?.Dispose();

            if (!Directory.Exists(this._baseDirectory))
            {
                try
                {
                    Directory.CreateDirectory(this._baseDirectory);
                }
                catch
                {
                    this._writer = null;
                    return;
                }
            }

            this._currentFileDate = today;
            this._currentFileIndex = 0;
            this._currentFilePath = this.GetFilePath(today, this._currentFileIndex);

            try
            {
                this._writer = new StreamWriter(this._currentFilePath, true, Encoding.UTF8)
                {
                    AutoFlush = false,
                };
            }
            catch
            {
                this._writer = null;
            }
        }

        private void RollIfNeeded()
        {
            try
            {
                var fileInfo = new FileInfo(this._currentFilePath);
                if (fileInfo.Length < this.MaxFileSizeBytes)
                {
                    return;
                }

                this._writer?.Dispose();
                this._currentFileIndex++;
                this._currentFilePath = this.GetFilePath(this._currentFileDate, this._currentFileIndex);
                this._writer = new StreamWriter(this._currentFilePath, true, Encoding.UTF8)
                {
                    AutoFlush = false,
                };
            }
            catch
            {
                // Ignore roll errors; next write will attempt to recover.
            }
        }

        private string GetFilePath(DateTime date, int index)
        {
            var suffix = index == 0 ? string.Empty : $"-{index}";
            return Path.Combine(this._baseDirectory, $"{this._fileNamePrefix}{date:yyyyMMdd}{suffix}{this._fileExtension}");
        }

        private SmartHopperSettings? GetSettings()
        {
            try
            {
                return SmartHopperSettings.Instance;
            }
            catch
            {
                return null;
            }
        }
    }
}
