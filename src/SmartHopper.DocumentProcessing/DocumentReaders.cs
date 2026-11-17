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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartHopper.DocumentProcessing.Readers;

namespace SmartHopper.DocumentProcessing
{
    /// <summary>
    /// Registry dispatching source files to the <see cref="IDocumentReader"/> that owns their
    /// extension. Built-in readers are pre-registered; additional formats (e.g. DOCX, PPTX)
    /// plug in through <see cref="Register"/> without touching call sites.
    /// </summary>
    public static class DocumentReaders
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<IDocumentReader> Registered = new List<IDocumentReader>
        {
            new PdfDocumentReader(),
        };

        /// <summary>All registered readers (snapshot).</summary>
        public static IReadOnlyList<IDocumentReader> All
        {
            get
            {
                lock (SyncRoot)
                {
                    return Registered.ToArray();
                }
            }
        }

        /// <summary>
        /// Registers a reader. A reader whose concrete type is already registered is skipped,
        /// so registration is idempotent and thread-safe.
        /// </summary>
        /// <param name="reader">The reader to register.</param>
        public static void Register(IDocumentReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            lock (SyncRoot)
            {
                var type = reader.GetType();
                if (Registered.All(r => r.GetType() != type))
                {
                    Registered.Add(reader);
                }
            }
        }

        /// <summary>
        /// Resolves the reader for a source path (its extension is used) or a bare extension
        /// with or without leading dot.
        /// </summary>
        public static bool TryGetReader(string sourcePathOrExtension, out IDocumentReader reader)
        {
            var extension = NormalizeExtension(sourcePathOrExtension);
            if (extension != null)
            {
                lock (SyncRoot)
                {
                    reader = Registered.FirstOrDefault(r =>
                        r.SupportedExtensions.Any(e =>
                            string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)));
                    return reader != null;
                }
            }

            reader = null;
            return false;
        }

        /// <summary>
        /// Resolves the reader for <paramref name="sourcePathOrExtension"/> or throws
        /// <see cref="NotSupportedException"/> listing the supported extensions.
        /// </summary>
        public static IDocumentReader GetReader(string sourcePathOrExtension)
        {
            if (TryGetReader(sourcePathOrExtension, out var reader))
            {
                return reader;
            }

            var supported = string.Join(", ",
                All.SelectMany(r => r.SupportedExtensions).OrderBy(e => e, StringComparer.OrdinalIgnoreCase));
            throw new NotSupportedException(
                $"No document reader is registered for '{sourcePathOrExtension}'. Supported extensions: {supported}.");
        }

        private static string NormalizeExtension(string sourcePathOrExtension)
        {
            if (string.IsNullOrWhiteSpace(sourcePathOrExtension))
            {
                return null;
            }

            var value = sourcePathOrExtension.Trim();
            var extension = value.StartsWith(".", StringComparison.Ordinal)
                ? value
                : Path.GetExtension(value);
            return string.IsNullOrEmpty(extension) ? null : extension;
        }
    }
}
