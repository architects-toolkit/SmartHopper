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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Core.Grasshopper.GhJsonSpec
{
    /// <summary>
    /// Loads the GhJSON and GhPatch specification markdown documents from embedded resources,
    /// with optional online fallback to the published ghjson-spec repository.
    /// </summary>
    public static class GhJsonSpecLoader
    {
        /// <summary>
        /// The default specification version used when none is specified.
        /// </summary>
        public const string DefaultVersion = "1.0";

        private const string SpecificationFileName = "specification.md";
        private const string PatchFileName = "ghpatch.md";
        private const string BaseRawUrl = "https://raw.githubusercontent.com/architects-toolkit/ghjson-spec/main/docs/";

        private static readonly Lazy<HttpClient> LazyHttpClient = new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var uaVersion = typeof(GhJsonSpecLoader).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? DefaultVersion;
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SmartHopper", uaVersion));
            return client;
        });

        private static readonly Dictionary<string, (string Document, string Heading)> TopicMap =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "document_structure", (SpecificationFileName, "Document Structure") },
            { "components", (SpecificationFileName, "Components") },
            { "connections", (SpecificationFileName, "Connections") },
            { "groups", (SpecificationFileName, "Groups") },
            { "data_types", (SpecificationFileName, "Data Types") },
            { "component_specific_formats", (SpecificationFileName, "Component-Specific Formats") },
            { "validation", (SpecificationFileName, "Validation") },
            { "examples", (SpecificationFileName, "Examples") },
        };

        /// <summary>
        /// Loads the full GhJSON specification markdown document.
        /// </summary>
        /// <param name="preferOnline">When <c>true</c>, fetch from the online repository first and fall back to embedded resources on failure.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The full GhJSON specification markdown.</returns>
        public static async Task<string> LoadSpecificationAsync(bool preferOnline = false, CancellationToken cancellationToken = default)
        {
            return await LoadDocumentAsync(SpecificationFileName, preferOnline, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads the full GhPatch specification markdown document.
        /// </summary>
        /// <param name="preferOnline">When <c>true</c>, fetch from the online repository first and fall back to embedded resources on failure.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The full GhPatch specification markdown.</returns>
        public static async Task<string> LoadPatchSpecificationAsync(bool preferOnline = false, CancellationToken cancellationToken = default)
        {
            return await LoadDocumentAsync(PatchFileName, preferOnline, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads a specific reference topic from the GhJSON/GhPatch specifications.
        /// </summary>
        /// <param name="topic">The topic to retrieve. Supported values include: overview, specification, ghpatch, document_structure, components, connections, groups, data_types, component_specific_formats, validation, examples.</param>
        /// <param name="preferOnline">When <c>true</c>, fetch from the online repository first and fall back to embedded resources on failure.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Markdown-formatted reference documentation for the requested topic.</returns>
        public static async Task<string> LoadTopicAsync(string topic, bool preferOnline = false, CancellationToken cancellationToken = default)
        {
            var normalizedTopic = topic?.Trim().ToLowerInvariant() ?? string.Empty;

            switch (normalizedTopic)
            {
                case "overview":
                    return await LoadOverviewAsync(preferOnline, cancellationToken).ConfigureAwait(false);
                case "specification":
                    return await LoadSpecificationAsync(preferOnline, cancellationToken).ConfigureAwait(false);
                case "ghpatch":
                    return await LoadPatchSpecificationAsync(preferOnline, cancellationToken).ConfigureAwait(false);
                default:
                    if (TopicMap.TryGetValue(normalizedTopic, out var mapping))
                    {
                        var document = await LoadDocumentAsync(mapping.Document, preferOnline, cancellationToken).ConfigureAwait(false);
                        return ExtractSection(document, mapping.Heading);
                    }

                    return $"Unknown topic '{topic}'. Available topics: overview, specification, ghpatch, document_structure, components, connections, groups, data_types, component_specific_formats, validation, examples.";
            }
        }

        private static async Task<string> LoadOverviewAsync(bool preferOnline, CancellationToken cancellationToken)
        {
            var spec = await LoadDocumentAsync(SpecificationFileName, preferOnline, cancellationToken).ConfigureAwait(false);
            var patch = await LoadDocumentAsync(PatchFileName, preferOnline, cancellationToken).ConfigureAwait(false);

            var specIntro = ExtractSection(spec, "Introduction");
            var patchIntro = ExtractSection(patch, "Introduction");

            var lines = new List<string>
            {
                "# GhJSON and GhPatch overview",
                string.Empty,
                "This overview combines the introductions from the GhJSON and GhPatch specifications. Use the `specification` topic for the full GhJSON document and the `ghpatch` topic for the full GhPatch document.",
                string.Empty,
                "---",
                string.Empty,
                specIntro,
                string.Empty,
                "---",
                string.Empty,
                patchIntro,
            };

            return string.Join(Environment.NewLine, lines);
        }

        private static async Task<string> LoadDocumentAsync(string fileName, bool preferOnline, CancellationToken cancellationToken)
        {
            if (!preferOnline)
            {
                try
                {
                    return LoadEmbedded(fileName);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Debug.WriteLine($"[GhJsonSpecLoader] Embedded load failed for '{fileName}': {ex.Message}");
                }
            }

            try
            {
                return await LoadOnlineAsync(fileName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                if (!preferOnline)
                {
                    Debug.WriteLine($"[GhJsonSpecLoader] Online load failed for '{fileName}', rethrowing: {ex.Message}");
                    throw;
                }

                Debug.WriteLine($"[GhJsonSpecLoader] Online load failed for '{fileName}', falling back to embedded: {ex.Message}");
                return LoadEmbedded(fileName);
            }
        }

        private static string LoadEmbedded(string fileName)
        {
            var assembly = typeof(GhJsonSpecLoader).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();

            // Discover the resource by path fragment and file name so we do not depend on the
            // exact MSBuild encoding of dots in folder names (v1.0 may become v1._0).
            var resourceName = resourceNames
                .Where(n => n.IndexOf("GhJsonSpec", StringComparison.OrdinalIgnoreCase) >= 0
                            && n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.Length)
                .FirstOrDefault();

            if (resourceName == null)
            {
                throw new FileNotFoundException(
                    $"Embedded GhJSON spec resource not found for '{fileName}'. Available resources: {string.Join(", ", resourceNames)}.");
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded GhJSON spec resource stream not found: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            Debug.WriteLine($"[GhJsonSpecLoader] Loaded '{fileName}' from embedded resource '{resourceName}' ({content.Length} chars).");
            return content;
        }

        private static async Task<string> LoadOnlineAsync(string fileName, CancellationToken cancellationToken)
        {
            var url = BaseRawUrl + fileName;
            var client = LazyHttpClient.Value;

            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Debug.WriteLine($"[GhJsonSpecLoader] Loaded '{fileName}' from online source '{url}' ({content.Length} chars).");
            return content;
        }

        private static string ExtractSection(string markdown, string headingTitle)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var startIndex = -1;
            var startLevel = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var (level, title) = ParseHeading(lines[i]);
                if (level <= 0)
                {
                    continue;
                }

                if (startIndex == -1 && string.Equals(title, headingTitle, StringComparison.OrdinalIgnoreCase))
                {
                    startIndex = i;
                    startLevel = level;
                }
                else if (startIndex != -1 && level <= startLevel)
                {
                    return string.Join(Environment.NewLine, lines.Skip(startIndex).Take(i - startIndex));
                }
            }

            if (startIndex != -1)
            {
                return string.Join(Environment.NewLine, lines.Skip(startIndex));
            }

            return $"Section '{headingTitle}' not found in the specification.";
        }

        private static (int Level, string Title) ParseHeading(string line)
        {
            var match = Regex.Match(line.TrimStart(), @"^(#{1,6})\s*(?:\d+(?:\.\d*)?\s+)?(.*)$");
            if (match.Success)
            {
                var level = match.Groups[1].Value.Length;
                var title = match.Groups[2].Value.Trim();
                return (level, title);
            }

            return (0, string.Empty);
        }
    }
}
