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

/*
 * Portions of this code inspired by:
 * https://github.com/deanmalmgren/textract
 * MIT License
 * Copyright (c) Dean Malmgren
 *
 * Key concepts adapted:
 * - IFileConverter interface pattern (dispatch-based file conversion)
 * - Extension-based converter routing
 */

using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Interface for file-to-markdown converters.
    /// Each converter handles one or more file formats.
    /// </summary>
    public interface IFileConverter
    {
        /// <summary>
        /// Gets the file extensions supported by this converter (e.g., ".pdf", ".docx").
        /// Extensions should include the leading dot and be lowercase.
        /// </summary>
        IEnumerable<string> SupportedExtensions { get; }

        /// <summary>
        /// Converts a file to Markdown asynchronously.
        /// </summary>
        /// <param name="filePath">Absolute path to the file to convert.</param>
        /// <param name="options">Conversion options.</param>
        /// <returns>Conversion result containing Markdown content and metadata.</returns>
        Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options);
    }
}
