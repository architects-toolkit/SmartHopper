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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for EPUB files (.epub).
    /// Extracts chapters in reading order and converts XHTML to plain text.
    /// </summary>
    public sealed class EpubConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".epub" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "epub");

                    using var archive = ZipFile.OpenRead(filePath);

                    // Find content.opf
                    var containerEntry = archive.GetEntry("META-INF/container.xml");
                    if (containerEntry == null)
                    {
                        return FileConversionResult.Failure("epub", "Invalid EPUB: META-INF/container.xml not found.");
                    }

                    string opfPath;
                    using (var stream = containerEntry.Open())
                    {
                        var containerDoc = XDocument.Load(stream);
                        var ns = containerDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                        var rootfile = containerDoc.Descendants(ns + "rootfile").FirstOrDefault();
                        opfPath = rootfile?.Attribute("full-path")?.Value ?? "content.opf";
                    }

                    // Read content.opf
                    var opfEntry = archive.GetEntry(opfPath);
                    if (opfEntry == null)
                    {
                        return FileConversionResult.Failure("epub", $"Invalid EPUB: {opfPath} not found.");
                    }

                    XDocument opfDoc;
                    using (var stream = opfEntry.Open())
                    {
                        opfDoc = XDocument.Load(stream);
                    }

                    var opfNs = opfDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                    // Extract metadata
                    var metadata = opfDoc.Descendants(opfNs + "metadata").FirstOrDefault();
                    if (metadata != null)
                    {
                        var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");
                        var title = metadata.Element(dcNs + "title")?.Value;
                        var creator = metadata.Element(dcNs + "creator")?.Value;

                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            result.Metadata["title"] = title;
                            markdown.Append("# ").AppendLine(title);
                            markdown.AppendLine();
                        }

                        if (!string.IsNullOrWhiteSpace(creator))
                        {
                            result.Metadata["author"] = creator;
                            markdown.Append("**Author:** ").AppendLine(creator);
                            markdown.AppendLine();
                        }
                    }

                    // Get spine (reading order)
                    var spine = opfDoc.Descendants(opfNs + "spine").FirstOrDefault();
                    if (spine == null)
                    {
                        return FileConversionResult.Failure("epub", "Invalid EPUB: spine not found.");
                    }

                    var manifest = opfDoc.Descendants(opfNs + "manifest").FirstOrDefault();
                    if (manifest == null)
                    {
                        return FileConversionResult.Failure("epub", "Invalid EPUB: manifest not found.");
                    }

                    // Build id -> href map
                    var manifestItems = manifest.Elements(opfNs + "item")
                        .ToDictionary(
                            item => item.Attribute("id")?.Value ?? string.Empty,
                            item => item.Attribute("href")?.Value ?? string.Empty);

                    // Get base path for content files
                    var opfDir = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? string.Empty;

                    // Process spine items in order
                    var itemrefs = spine.Elements(opfNs + "itemref").ToList();
                    int chapterNumber = 1;
                    foreach (var itemref in itemrefs)
                    {
                        var idref = itemref.Attribute("idref")?.Value;
                        if (string.IsNullOrEmpty(idref) || !manifestItems.TryGetValue(idref, out var href))
                        {
                            continue;
                        }

                        var contentPath = string.IsNullOrEmpty(opfDir) ? href : $"{opfDir}/{href}";
                        var contentEntry = archive.GetEntry(contentPath);
                        if (contentEntry == null)
                        {
                            continue;
                        }

                        string chapterText;
                        using (var stream = contentEntry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            var xhtml = reader.ReadToEnd();
                            chapterText = ConvertXhtmlToText(xhtml);
                        }

                        if (!string.IsNullOrWhiteSpace(chapterText))
                        {
                            if (itemrefs.Count > 1)
                            {
                                markdown.Append("## Chapter ").AppendLine(chapterNumber.ToString());
                                markdown.AppendLine();
                            }

                            markdown.AppendLine(chapterText.Trim());
                            markdown.AppendLine();
                            markdown.AppendLine("---");
                            markdown.AppendLine();
                            chapterNumber++;
                        }
                    }

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("epub", $"Failed to convert EPUB: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static string ConvertXhtmlToText(string xhtml)
        {
            try
            {
                var doc = XDocument.Parse(xhtml);
                var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
                return body?.Value ?? xhtml;
            }
            catch
            {
                // If parsing fails, strip tags with regex
                var text = System.Text.RegularExpressions.Regex.Replace(xhtml, @"<[^>]+>", string.Empty);
                return System.Net.WebUtility.HtmlDecode(text);
            }
        }
    }
}
