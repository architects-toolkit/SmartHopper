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
using System.IO;
using System.Linq;
using System.Reflection;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Loads Markdown content embedded in the <see cref="SmartHopper.Infrastructure"/> assembly.
    /// </summary>
    internal static class EmbeddedMcpResourceLoader
    {
        private const string ResourcePathFragment = "Resources.Mcp";

        private static readonly Lazy<string[]> ManifestResourceNames = new (() =>
            typeof(EmbeddedMcpResourceLoader).GetTypeInfo().Assembly.GetManifestResourceNames());

        private static readonly ConcurrentDictionary<string, string> ResolvedResourceNames =
            new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reads an embedded Markdown file by its file name (e.g. <c>grasshopper-expert.md</c>).
        /// </summary>
        /// <param name="fileName">The Markdown file name, including its extension.</param>
        /// <returns>The file contents.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <c>null</c>.</exception>
        /// <exception cref="FileNotFoundException">The requested resource is not embedded in the assembly.</exception>
        public static string ReadMarkdown(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var resourceName = ResolvedResourceNames.GetOrAdd(fileName, ResolveResourceName);
            var assembly = typeof(EmbeddedMcpResourceLoader).GetTypeInfo().Assembly;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded MCP resource stream not found: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string ResolveResourceName(string fileName)
        {
            // Discover the resource by path fragment and file name so we do not depend on the
            // exact MSBuild encoding of dots in folder names.
            var resourceNames = ManifestResourceNames.Value;
            var resourceName = resourceNames
                .Where(n =>
                    n.Contains(ResourcePathFragment, StringComparison.OrdinalIgnoreCase) &&
                    n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.Length)
                .FirstOrDefault();

            return resourceName ?? throw new FileNotFoundException(
                $"Embedded MCP resource not found for '{fileName}'. Available resources: {string.Join(", ", resourceNames)}.");
        }
    }
}
