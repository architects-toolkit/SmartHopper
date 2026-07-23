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
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Utilities
{
    /// <summary>
    /// Shared utilities for working with SHRuntimeMessage collections.
    /// </summary>
    public static class RuntimeMessageUtility
    {
        /// <summary>
        /// Checks if a collection of runtime messages contains at least one message
        /// with severity at or above the specified threshold.
        /// </summary>
        /// <param name="messages">The messages to check.</param>
        /// <param name="threshold">The minimum severity level to match.</param>
        /// <returns>True if any message has severity >= threshold; otherwise false.</returns>
        public static bool HasSeverityAtOrAbove(List<SHRuntimeMessage> messages, SHRuntimeMessageSeverity threshold)
        {
            if (messages == null)
            {
                return false;
            }

            foreach (var m in messages)
            {
                if (m != null && m.Severity >= threshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts runtime messages from a JObject tool result.
        /// Looks for standard message fields like "messages", "errors", "warnings", or "__envelope.messages".
        /// </summary>
        /// <param name="toolResult">The JObject to extract messages from.</param>
        /// <returns>A list of extracted SHRuntimeMessage objects, or empty list if none found.</returns>
        public static List<SHRuntimeMessage> ExtractMessages(JObject toolResult)
        {
            var messages = new List<SHRuntimeMessage>();

            if (toolResult == null)
            {
                return messages;
            }

            try
            {
                // Check for direct "messages" array
                if (toolResult["messages"] is JArray messagesArray)
                {
                    ExtractFromArray(messagesArray, messages);
                }

                // Check for "errors" array
                if (toolResult["errors"] is JArray errorsArray)
                {
                    ExtractFromArray(errorsArray, messages, SHRuntimeMessageSeverity.Error);
                }

                // Check for "warnings" array
                if (toolResult["warnings"] is JArray warningsArray)
                {
                    ExtractFromArray(warningsArray, messages, SHRuntimeMessageSeverity.Warning);
                }

                // Check for envelope messages
                if (toolResult["__envelope"]?["messages"] is JArray envelopeMessages)
                {
                    ExtractFromArray(envelopeMessages, messages);
                }
            }
            catch (Exception ex)
            {
                // Best-effort extraction; log but don't throw
                System.Diagnostics.Debug.WriteLine($"[RuntimeMessageUtility.ExtractMessages] Error extracting messages: {ex.Message}");
            }

            return messages;
        }

        /// <summary>
        /// Helper to extract messages from a JArray.
        /// </summary>
        private static void ExtractFromArray(JArray array, List<SHRuntimeMessage> messages, SHRuntimeMessageSeverity? defaultSeverity = null)
        {
            foreach (var item in array)
            {
                if (item == null)
                {
                    continue;
                }

                try
                {
                    string text = null;
                    SHRuntimeMessageSeverity severity = defaultSeverity ?? SHRuntimeMessageSeverity.Info;

                    // Try to extract as string
                    if (item.Type == JTokenType.String)
                    {
                        text = item.Value<string>();
                    }
                    else if (item is JObject msgObj)
                    {
                        // Try to extract from object with "text", "message", or "content" field
                        text = msgObj["text"]?.Value<string>()
                            ?? msgObj["message"]?.Value<string>()
                            ?? msgObj["content"]?.Value<string>();

                        // Try to extract severity
                        var severityStr = msgObj["severity"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(severityStr) && Enum.TryParse<SHRuntimeMessageSeverity>(severityStr, ignoreCase: true, out var parsedSeverity))
                        {
                            severity = parsedSeverity;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        messages.Add(new SHRuntimeMessage(
                            severity,
                            SHRuntimeMessageOrigin.Tool,
                            SHMessageCode.Unknown,
                            text));
                    }
                }
                catch
                {
                    // Skip items that can't be parsed
                }
            }
        }
    }
}
