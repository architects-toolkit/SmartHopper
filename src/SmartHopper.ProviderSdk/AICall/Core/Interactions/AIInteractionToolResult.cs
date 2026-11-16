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

using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated tool result with associated metadata.
    /// </summary>
    public sealed record AIInteractionToolResult : AIInteractionToolCall, IAIInteraction, IAIRenderInteraction
    {
        /// <inheritdoc/>
        public override AIAgent Agent { get; init; } = AIAgent.ToolResult;

        /// <summary>
        /// Gets the result of the tool call.
        /// </summary>
        public JObject Result { get; init; }

        /// <summary>
        /// Gets the image payloads produced by this tool result. These are extracted from the
        /// result JSON by <see cref="ToolResultMediaExtractor"/> so the image stays attached to
        /// the tool result rather than being serialized as base64 text to providers.
        /// </summary>
        public List<ToolResultImage> Images { get; init; } = new List<ToolResultImage>();

        /// <summary>
        /// Gets the structured runtime messages produced while generating this tool result.
        /// These are propagated from inner AI calls to improve diagnostics and visibility.
        /// </summary>
        public List<SHRuntimeMessage> Messages { get; init; } = new List<SHRuntimeMessage>();

        /// <summary>
        /// Returns a string representation of the <see cref="AIInteractionToolResult"/>.
        /// </summary>
        /// <returns>A formatted string containing tool result metadata.</returns>
        public override string ToString()
        {
            var result = "Tool result";

            if (!string.IsNullOrEmpty(this.Name))
            {
                result += $" from {this.Name}";
            }

            if (this.Result != null && this.Result.HasValues)
            {
                result += $":\n{JsonConvert.SerializeObject(this.Result, Formatting.Indented)}";
            }

            return result;
        }

        /// <summary>
        /// Returns the originating tool call's stream key so the chat merges the
        /// result into the call's card: the pending card upserts under
        /// tool.call:{id} and this result upserts into that same DOM node,
        /// replacing the spinner header with the completed title.
        /// </summary>
        /// <returns>Stream group key shared with the originating call.</returns>
        public override string GetStreamKey()
        {
            return base.GetStreamKey();
        }

        /// <summary>
        /// Returns the originating tool call's de-duplication key so every
        /// render path (live upserts, history replay, final flush) collapses
        /// call and result into the same chat card.
        /// </summary>
        /// <returns>De-duplication key shared with the originating call.</returns>
        public override string GetDedupKey()
        {
            return base.GetDedupKey();
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction.
        /// </summary>
        public override string GetRoleClassForRender()
        {
            return "tool";
        }

        /// <summary>
        /// Gets the completed-state header for the merged card. Resolution order:
        /// the tool-written <c>summary</c> field in the result (it knows the real
        /// outcome — e.g. "found 12 components"), the model-written <c>aim_done</c>
        /// argument from the call, the tool's deterministic <c>ResultLabel</c>, and
        /// finally "Tool Result: {name}". All sentences must stay under seven words.
        /// Emits <c>data-state="error"</c> when the result reports a failure
        /// (<c>error</c> field or <c>success: false</c>) so the status glyph
        /// renders a cross instead of a tick.
        /// </summary>
        public override string GetDisplayNameForRender()
        {
            var label = SanitizeActivityLabel(this.Result?["summary"]?.ToString())
                ?? SanitizeActivityLabel(this.Arguments?["aim_done"]?.ToString())
                ?? this.GetResultLabelForRender();
            var title = label
                ?? (string.IsNullOrWhiteSpace(this.Name) ? "Tool Result" : $"Tool Result: {this.Name}");

            var callId = SanitizeActivityLabel(this.Id) ?? string.Empty;
            var state = this.IsErrorResult() ? "error" : "done";
            return $"<span class=\"cui-tool-title\" data-callid=\"{callId}\" data-state=\"{state}\">{title}</span>";
        }

        /// <summary>
        /// Detects a failed tool result: tools report failures either through an
        /// <c>error</c> message field (consent rejections, validation failures) or
        /// an explicit <c>success: false</c> (synthetic fallback results).
        /// </summary>
        internal bool IsErrorResult()
        {
            if (this.Result == null)
            {
                return false;
            }

            if (this.Result["error"] != null)
            {
                return true;
            }

            return this.Result["success"] is JValue { Type: JTokenType.Boolean } success && !success.Value<bool>();
        }

        /// <summary>
        /// Resolves the tool's deterministic completed-state sentence via the
        /// registered tool catalog, when one is declared.
        /// </summary>
        private string? GetResultLabelForRender()
        {
            if (!string.IsNullOrWhiteSpace(this.Name)
                && ProviderSdkHost.ToolRegistry.GetTools().TryGetValue(this.Name, out var tool))
            {
                return SanitizeActivityLabel(tool.ResultLabel);
            }

            return null;
        }

        /// <summary>
        /// Gets the raw content for the merged call+result card: a one-line digest
        /// (<c>name(params) → done|failed</c>) peeks out of the collapsed card, a
        /// collapsible parameter table follows when the call carried structured
        /// arguments, failures surface as a visible error callout, and the result
        /// renders as colorized pretty JSON inside an open details block with a
        /// "Copy raw" button carrying the compact wire payload in
        /// <c>data-copy</c> (the chat script forwards it through the
        /// <c>clipboard://</c> bridge). One markdown image per attached
        /// <see cref="ToolResultImage"/> trails the card.
        /// </summary>
        public override string GetRawContentForRender()
        {
            var sb = new StringBuilder();
            AppendCallDigestLine(sb, this.Name, this.Arguments, this.IsErrorResult() ? "failed" : "done");

            if (NeedsStructuredArgs(this.Arguments))
            {
                AppendArgumentsDetails(sb, this.Arguments);
            }

            var error = this.Result?["error"]?.ToString();
            if (!string.IsNullOrEmpty(error))
            {
                sb.Append("<div class=\"tool-error\">").Append(EncodeHtml(error)).Append("</div>");
            }

            if (this.Result != null && this.Result.HasValues)
            {
                var compact = JsonConvert.SerializeObject(this.Result, Formatting.None);
                sb.Append("<details class=\"tool-io tool-result\" open><summary>Result <span class=\"tool-size\">")
                    .Append(FormatByteCount(compact.Length))
                    .Append("</span></summary>")
                    .Append("<div class=\"tool-io-bar\"><button type=\"button\" class=\"cui-copy-raw\" data-copy=\"")
                    .Append(EncodeHtml(compact))
                    .Append("\">Copy raw</button></div>")
                    .Append("<pre><code class=\"language-json\">");
                AppendJsonHtml(sb, this.Result, 0);
                sb.Append("</code></pre></details>");
            }

            if (this.Images == null || this.Images.Count == 0)
            {
                return sb.ToString();
            }

            var label = string.IsNullOrWhiteSpace(this.Name) ? "Tool result image" : $"{this.Name} image";
            foreach (var image in this.Images)
            {
                if (string.IsNullOrEmpty(image.ImageData))
                {
                    continue;
                }

                sb.Append("\n\n![").Append(label).Append("](data:")
                    .Append(image.MimeType ?? "image/png")
                    .Append(";base64,")
                    .Append(image.ImageData)
                    .Append(')');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the plain-text payload carried by the card's copy source: the
        /// compact <c>args → result</c> summary followed by the fenced pretty
        /// result document, preserving the readable copy semantics from before
        /// the card moved to structured HTML.
        /// </summary>
        public override string GetCopyTextForRender()
        {
            var argsCompact = this.Arguments != null && this.Arguments.HasValues
                ? JsonConvert.SerializeObject(this.Arguments, Formatting.None)
                : "{}";

            if (this.Result == null || !this.Result.HasValues)
            {
                return $"``{argsCompact}``";
            }

            var resultCompact = JsonConvert.SerializeObject(this.Result, Formatting.None);
            var resultIndented = JsonConvert.SerializeObject(this.Result, Formatting.Indented);
            return $"``{argsCompact}`` → ``{resultCompact}``\n\n```json\n{resultIndented}\n```";
        }

        /// <summary>
        /// Formats a byte count for the result details header (B, KB, MB).
        /// </summary>
        private static string FormatByteCount(int bytes)
        {
            const int kb = 1024;
            const int mb = 1024 * 1024;
            if (bytes >= mb)
            {
                return $"{bytes / (double)mb:F1} MB";
            }

            return bytes >= kb ? $"{bytes / (double)kb:F1} KB" : $"{bytes} B";
        }

        /// <summary>
        /// Tool results do not include reasoning by default.
        /// </summary>
        public override string GetRawReasoningForRender()
        {
            return string.Empty;
        }
    }
}
