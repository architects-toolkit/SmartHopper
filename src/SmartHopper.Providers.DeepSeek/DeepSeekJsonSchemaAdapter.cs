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
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek JSON schema adapter. Inherits the standard OpenAI-compatible wrapping
    /// and only overrides <see cref="Unwrap"/> to clean up malformed enum arrays that
    /// some DeepSeek responses produce.
    /// </summary>
    internal sealed partial class DeepSeekJsonSchemaAdapter : OpenAICompatibleJsonSchemaAdapter
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for extracting enum arrays from malformed JSON.
        /// </summary>
        [GeneratedRegex(@"enum[""']?:\s*\[([^\]]+)\]")]
        private static partial Regex EnumArrayRegex();

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepSeekJsonSchemaAdapter"/> class.
        /// </summary>
        public DeepSeekJsonSchemaAdapter()
            : base("DeepSeek")
        {
        }

        /// <inheritdoc/>
        public override string Unwrap(string content, SchemaWrapperInfo info)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            // DeepSeek sometimes returns malformed JSON where an array is put under an "enum" property
            try
            {
                var trimmed = content.TrimStart();
                if (trimmed.StartsWith("{"))
                {
                    var obj = JObject.Parse(content);
                    if (obj["enum"] is JArray enumArray)
                    {
                        var cleanedArray = enumArray.ToString(Newtonsoft.Json.Formatting.None);
                        Debug.WriteLine($"[DeepSeekAdapter] Cleaned enum array: {cleanedArray}");
                        return cleanedArray;
                    }
                }
            }
            catch
            {
                // Fall through to regex attempt
            }

            try
            {
                // Fallback: try regex extraction if JSON parsing fails
                var match = EnumArrayRegex().Match(content);
                if (match.Success)
                {
                    var inner = match.Groups[1].Value;
                    var cleaned = $"[{inner}]";
                    Debug.WriteLine($"[DeepSeekAdapter] Regex extracted enum array: {cleaned}");
                    return cleaned;
                }
            }
            catch
            {
                // ignore
            }

            return content;
        }
    }
}
