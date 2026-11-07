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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.Hosting;
namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated tool call with associated metadata.
    /// </summary>
    public record AIInteractionToolCall : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <inheritdoc/>
        public override AIAgent Agent { get; init; } = AIAgent.ToolCall;

        /// <summary>
        /// Gets the id of the tool call.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets the name of the tool to call.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the arguments of the tool call.
        /// </summary>
        public JObject Arguments { get; init; }

        /// <summary>
        /// Gets the reasoning content associated with this tool call.
        /// Used by providers like DeepSeek that include reasoning_content with tool calls.
        /// </summary>
        public string Reasoning { get; init; }

        /// <summary>
        /// Returns a string representation of the <see cref="AIInteractionToolCall"/>.
        /// </summary>
        /// <returns>A formatted string containing tool call metadata.</returns>
        public override string ToString()
        {
            var result = "Calling tool";

            if (!string.IsNullOrEmpty(this.Id))
            {
                result += $" ({this.Id})";
            }

            if (!string.IsNullOrEmpty(this.Name))
            {
                result += $" {this.Name}";
            }

            if (this.Arguments != null && this.Arguments.HasValues)
            {
                result += $" with the following arguments:\n{JsonConvert.SerializeObject(this.Arguments, Formatting.Indented)}";
            }

            return result;
        }

        /// <summary>
        /// Returns a stable stream grouping key for this interaction. Defaults to tool.call:{IdOrName}.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public virtual string GetStreamKey()
        {
            var id = !string.IsNullOrEmpty(this.Id) ? this.Id : (this.Name ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:tool.call:{id}";
            }

            return $"tool.call:{id}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this interaction. Includes arguments hash to disambiguate.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public virtual string GetDedupKey()
        {
            var id = !string.IsNullOrEmpty(this.Id) ? this.Id : (this.Name ?? string.Empty);
            var argsStr = this.Arguments != null ? this.Arguments.ToString(Newtonsoft.Json.Formatting.None) : string.Empty;
            var argsHash = !string.IsNullOrEmpty(argsStr) ? SmartHopper.ProviderSdk.AICall.Utilities.HashUtility.ComputeShortHash(argsStr) : "none";

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:tool.call:{id}:{argsHash}";
            }

            return $"tool.call:{id}:{argsHash}";
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction.
        /// </summary>
        public virtual string GetRoleClassForRender()
        {
            return "tool";
        }

        /// <summary>
        /// Gets the display name for rendering (header label): the activity
        /// sentence when the model supplied <c>aim</c> or the tool declares an
        /// <c>ActivityLabel</c>, else the tool name. Emits the shared tool-card
        /// contract (<c>data-callid</c>/<c>data-state="pending"</c>); the status
        /// glyph is a CSS <c>::before</c> on the title span driven by
        /// <c>data-state</c> — <c>pending</c> shows a hollow circle until the
        /// executor flips it to <c>running</c> (spinner), and the result upsert
        /// replaces the span with <c>done</c>/<c>error</c>.
        /// </summary>
        public virtual string GetDisplayNameForRender()
        {
            var name = this.GetActivityLabelForRender()
                ?? (string.IsNullOrWhiteSpace(this.Name) ? "Tool Call" : $"Tool Call: {this.Name}");

            var callId = SanitizeActivityLabel(this.Id) ?? string.Empty;
            return $"<span class=\"cui-tool-title\" data-callid=\"{callId}\" data-state=\"pending\">{name}</span>";
        }

        /// <summary>
        /// Gets the raw markdown content to render for this interaction. A compact
        /// single-line preview leads so the collapsed chat bubble shows real content
        /// instead of only the first "{" of the pretty-printed JSON block that follows.
        /// </summary>
        public virtual string GetRawContentForRender()
        {
            if (this.Arguments == null || !this.Arguments.HasValues)
            {
                return string.Empty;
            }

            var compact = JsonConvert.SerializeObject(this.Arguments, Formatting.None);
            var indented = JsonConvert.SerializeObject(this.Arguments, Formatting.Indented);
            return $"``{compact}``\n\n```json\n{indented}\n```";
        }

        /// <summary>
        /// Resolves the short activity sentence for the collapsed bubble: the
        /// model-written <c>aim</c> argument when present, else the tool's
        /// deterministic <c>ActivityLabel</c>. Only sentences shorter than seven
        /// words are honored.
        /// </summary>
        internal string? GetActivityLabelForRender()
        {
            var label = SanitizeActivityLabel(this.Arguments?["aim"]?.ToString());
            if (label != null)
            {
                return label;
            }

            if (!string.IsNullOrWhiteSpace(this.Name)
                && ProviderSdkHost.ToolRegistry.GetTools().TryGetValue(this.Name, out var tool))
            {
                return SanitizeActivityLabel(tool.ActivityLabel);
            }

            return null;
        }

        internal static string? SanitizeActivityLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            // Strip markup characters too: the label is injected raw into the
            // card header HTML, and 'aim' is model-controlled text.
            var cleaned = label.Replace('\n', ' ').Replace('\r', ' ')
                .Replace("`", string.Empty).Replace("*", string.Empty)
                .Replace("<", string.Empty).Replace(">", string.Empty)
                .Replace("&", string.Empty).Replace("\"", string.Empty).Trim();
            var wordCount = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return wordCount is > 0 and < 7 ? cleaned : null;
        }

        /// <summary>
        /// Gets the reasoning content associated with this tool call, if any.
        /// </summary>
        public virtual string GetRawReasoningForRender()
        {
            return this.Reasoning ?? string.Empty;
        }
    }
}
