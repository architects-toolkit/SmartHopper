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

#nullable enable

using System.Linq;
using Newtonsoft.Json.Linq;
namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Canonical finish reasons for provider calls, shared across API wire formats.
    /// Providers report termination differently depending on the API shape
    /// (Chat Completions uses <c>choices[].finish_reason</c>, the Responses API
    /// uses top-level <c>status</c> + <c>incomplete_details.reason</c>); every
    /// consumer — metrics decode, batch warnings, aggregation — must read the
    /// normalized values produced here so the same outcome carries the same
    /// reason regardless of the emitting API.
    /// </summary>
    public static class AIFinishReason
    {
        /// <summary>Normal termination. Chat Completions <c>stop</c>; Responses API <c>completed</c>.</summary>
        public const string Stop = "stop";

        /// <summary>
        /// Output truncated by the generation cap. Chat Completions <c>length</c>;
        /// Responses API <c>status="incomplete"</c> with <c>incomplete_details.reason="max_output_tokens"</c>.
        /// </summary>
        public const string Length = "length";

        /// <summary>Abnormal termination reported by neither wire format — produced locally for failed calls.</summary>
        public const string Error = "error";

        /// <summary>
        /// Extracts the canonical finish reason from an OpenAI-compatible response
        /// body. Reads <c>choices[0].finish_reason</c> first (Chat Completions),
        /// then the Responses API <c>status</c>: <c>completed</c> maps to
        /// <see cref="Stop"/>, <c>incomplete</c> resolves
        /// <c>incomplete_details.reason</c> (<c>max_output_tokens</c> maps to
        /// <see cref="Length"/>, any other detail surfaces verbatim), and every
        /// other status (<c>failed</c>, <c>cancelled</c>, <c>in_progress</c>, …)
        /// surfaces verbatim as a real abnormal reason.
        /// </summary>
        /// <param name="response">The provider response body.</param>
        /// <returns>The canonical finish reason, or <c>null</c> when the body reports none.</returns>
        public static string? FromOpenAIResponse(JObject? response)
        {
            if (response == null)
            {
                return null;
            }

            // Chat Completions: choices[0].finish_reason
            var choices = response["choices"] as JArray;
            var reason = (choices?.FirstOrDefault() as JObject)?["finish_reason"]?.ToString();
            if (!string.IsNullOrEmpty(reason))
            {
                return reason;
            }

            // Responses API: status + incomplete_details.reason
            var status = response["status"]?.ToString();
            switch (status)
            {
                case null:
                case "":
                    return null;
                case "completed":
                    return Stop;
                case "incomplete":
                {
                    var incomplete = (response["incomplete_details"] as JObject)?["reason"]?.ToString();
                    return incomplete == "max_output_tokens" ? Length
                        : string.IsNullOrEmpty(incomplete) ? status
                        : incomplete;
                }

                default:
                    return status;
            }
        }

        /// <summary>
        /// Whether the canonical reason is abnormal — anything other than
        /// <see cref="Stop"/>. Shared gate for finish-reason warnings so
        /// vocabulary differences between API shapes cannot produce false
        /// positives (e.g. a Responses API <c>completed</c> flagged as non-stop).
        /// </summary>
        /// <param name="reason">A canonical reason from <see cref="FromOpenAIResponse"/>.</param>
        public static bool IsAbnormal(string? reason)
            => !string.IsNullOrEmpty(reason) && reason != Stop;
    }
}
