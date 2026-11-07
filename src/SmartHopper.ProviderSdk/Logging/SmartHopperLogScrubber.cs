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
using System.Text.RegularExpressions;

namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// Centralized log redaction utility that scrubs credentials, tokens, emails,
    /// IP addresses, file paths, and GhJSON sensitive data from log output before it reaches any sink.
    /// </summary>
    public static class SmartHopperLogScrubber
    {
        private static readonly Regex[] ScrubPatterns;
        private static readonly string[] Replacements;

        static SmartHopperLogScrubber()
        {
            var options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

            var patterns = new List<Regex>();
            var replacements = new List<string>();

            // URLs with embedded credentials: https://user:pass@host
            patterns.Add(new Regex("(?i)(https?://)[^\\s:@/?#]+:[^\\s:@/?#]+@", options));
            replacements.Add("$1<redacted>@");

            // Authorization / Bearer / Basic headers
            patterns.Add(new Regex("(?i)(authorization\\s*(?::|=)\\s*)((?:bearer\\s+|basic\\s+)?[a-zA-Z0-9_\\+\\/\\=\\-\\.]+)", options));
            replacements.Add("$1<redacted>");

            // Standalone Bearer / Basic token
            patterns.Add(new Regex("(?i)\\b(bearer\\s+)([a-zA-Z0-9_\\+\\/\\=\\-\\.]+)", options));
            replacements.Add("$1<redacted>");

            // File paths and .ghjson files
            patterns.Add(new Regex(@"\b[A-Za-z]:\\[^\n\r]*", options));
            replacements.Add("[file path]");

            patterns.Add(new Regex(@"\\\\\?\\[^\n\r]*", options));
            replacements.Add("[file path]");

            patterns.Add(new Regex(@"\\\\[a-zA-Z0-9_.-]+\\[^\n\r]*", options));
            replacements.Add("[unc path]");

            patterns.Add(new Regex(@"(^|[^\w])(\/(?:home|Users|tmp|var|opt|mnt|data|documents?|downloads|desktop|root|etc|bin|lib|share|media)(?:\/[^\s\n\r]*)?)", options));
            replacements.Add("$1[file path]");

            patterns.Add(new Regex(@"[^\s\n\r]+\.ghjson\b", options));
            replacements.Add("[ghjson file]");

            // GhJSON sensitive key/value pairs and geometry
            patterns.Add(new Regex(BuildGhJsonPattern(), options));
            replacements.Add("$1: \"<redacted>\"");

            patterns.Add(new Regex(BuildGhJsonDataTypePattern(), options));
            replacements.Add("\"$1:<ghjson-redacted>\"");

            patterns.Add(new Regex(BuildJsonPattern(), options));
            replacements.Add("$1: \"<redacted>\"");

            patterns.Add(new Regex(BuildGenericPattern(), options));
            replacements.Add("$1$2=<redacted>");

            // Geometry coordinate arrays
            patterns.Add(new Regex(@"(?:\[|\(|\{)\s*-?\d+(?:\.\d+)?(?:\s*[ ,;]\s*-?\d+(?:\.\d+)?){2,}\s*(?:\]|\)|\})", options));
            replacements.Add("<geometry>");

            // Email addresses
            patterns.Add(new Regex("\\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}\\b", options));
            replacements.Add("<email>");

            // IPv4 addresses
            patterns.Add(new Regex("\\b(?:\\d{1,3}\\.){3}\\d{1,3}\\b", options));
            replacements.Add("<ip>");

            ScrubPatterns = patterns.ToArray();
            Replacements = replacements.ToArray();
        }

        /// <summary>
        /// Scrubs the input string, replacing sensitive values with safe placeholders.
        /// </summary>
        /// <param name="input">The raw log message or data.</param>
        /// <returns>The scrubbed string, or <see cref="string.Empty"/> if <paramref name="input"/> is null.</returns>
        public static string Scrub(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input ?? string.Empty;
            }

            var result = input;
            for (int i = 0; i < ScrubPatterns.Length; i++)
            {
                result = ScrubPatterns[i].Replace(result, Replacements[i]);
            }

            return result;
        }

        private static string BuildJsonPattern()
        {
            return "(?i)(\"\\s*(?:" + SensitiveKeyPattern + ")\\s*\")\\s*:\\s*\"[^\"]*\"";
        }

        private static string BuildGenericPattern()
        {
            return "(?i)(^|[\\s{,;?&])(" + SensitiveKeyPattern + ")\\b\\s*(?:=|:|=>)\\s*(?:\"[^\"]*\"|[^\"{,;\\s&?}])*";
        }

        private static string BuildGhJsonPattern()
        {
            return "(?i)(\"\\s*(?:" + GhJsonSensitiveKeyPattern + ")\\s*\")\\s*:\\s*\"[^\"]*\"";
        }

        private static string BuildGhJsonDataTypePattern()
        {
            return "\"(?:(pointXYZ|vectorXYZ|line2p|planeOXY|circleCNRS|arc3P|boxOXY|rectangleCXY|interval|bounds|text)):([^\"]*)\"";
        }

        private const string SensitiveKeyPattern =
            "api[_-]?key|apikey|x[-_]?api[-_]?key|x[-_]?goog[-_]?api[-_]?key|" +
            "bearer[_-]?token|token|secret|password|private[_-]?key|credential(?:s)?|connection[_-]?string|" +
            "database[_-]?url|pwd|passwd|auth[_-]?token|auth";

        private const string GhJsonSensitiveKeyPattern =
            "author|nickName|nick_name|description|pivot|text|code";
    }
}
