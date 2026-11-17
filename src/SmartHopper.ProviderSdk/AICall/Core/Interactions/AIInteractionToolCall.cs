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
using System.Net;
using System.Text;
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
        /// Gets the raw content to render for this interaction. Tool cards emit HTML
        /// directly (it passes Markdig untouched and survives the sanitizer's html
        /// profile): a one-line call digest peeks out of the collapsed card, followed
        /// by a collapsible structured parameter table. Calls with no arguments emit
        /// nothing — the header sentence already carries the whole summary.
        /// </summary>
        public virtual string GetRawContentForRender()
        {
            if (this.Arguments == null || !this.Arguments.HasValues)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            AppendCallDigestLine(sb, this.Name, this.Arguments, null);
            if (NeedsStructuredArgs(this.Arguments))
            {
                AppendArgumentsDetails(sb, this.Arguments);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the plain-text payload carried by the card's copy source
        /// (<c>data-copy-content</c>), which feeds the message-level copy handler.
        /// The card renders structured HTML, but copying a tool interaction should
        /// still yield readable content — not markup tags.
        /// </summary>
        public virtual string GetCopyTextForRender()
        {
            var label = string.IsNullOrWhiteSpace(this.Name) ? "Tool call" : $"Tool Call: {this.Name}";
            if (this.Arguments == null || !this.Arguments.HasValues)
            {
                return label;
            }

            var indented = JsonConvert.SerializeObject(this.Arguments, Formatting.Indented);
            return $"{label}\n\n```json\n{indented}\n```";
        }

        /// <summary>
        /// Appends the single-line call digest shown in the collapsed card peek:
        /// <c>name(p1: v1, p2: "s", +n more)</c>. Scalar values render inline
        /// (strings capped at 40 chars), complex values collapse to
        /// <c>{…}</c>/<c>[n]</c> placeholders, and label arguments
        /// (<c>aim</c>/<c>aim_done</c>) are skipped since they already appear in
        /// the card header. <paramref name="outcome"/> (e.g. "done"/"failed") is
        /// appended by tool results as <c> → outcome</c>.
        /// </summary>
        internal static void AppendCallDigestLine(StringBuilder sb, string name, JObject arguments, string outcome)
        {
            sb.Append("<p class=\"tool-call-line\"><code>");
            sb.Append(EncodeHtml(string.IsNullOrWhiteSpace(name) ? "call" : name));

            var paramCount = 0;
            var shown = 0;
            var extras = new StringBuilder();
            if (arguments != null)
            {
                foreach (var prop in arguments.Properties())
                {
                    if (prop.Name == "aim" || prop.Name == "aim_done")
                    {
                        continue;
                    }

                    paramCount++;
                    if (shown < 3)
                    {
                        extras.Append(shown == 0 ? "(" : ", ")
                            .Append(EncodeHtml(prop.Name))
                            .Append(": ")
                            .Append(EncodeHtml(DigestValue(prop.Value)));
                        shown++;
                    }
                }
            }

            var hidden = paramCount - shown;
            if (paramCount > 0)
            {
                sb.Append(extras);
                sb.Append(hidden > 0 ? $", +{hidden} more)" : ")");
            }

            if (!string.IsNullOrEmpty(outcome))
            {
                sb.Append(" → ").Append(EncodeHtml(outcome));
            }

            sb.Append("</code></p>");
        }

        /// <summary>
        /// True when the arguments need the structured parameter table: any nested
        /// object/array value, or more scalars than the digest line can show.
        /// </summary>
        internal static bool NeedsStructuredArgs(JObject arguments)
        {
            if (arguments == null || !arguments.HasValues)
            {
                return false;
            }

            var count = 0;
            foreach (var prop in arguments.Properties())
            {
                count++;
                if (prop.Value is JObject || prop.Value is JArray)
                {
                    return true;
                }
            }

            return count > 3;
        }

        /// <summary>
        /// Renders the collapsible parameter table. Scalar values render as inline
        /// code; object/array values become a nested details block holding the
        /// colorized JSON document.
        /// </summary>
        internal static void AppendArgumentsDetails(StringBuilder sb, JObject arguments)
        {
            sb.Append("<details class=\"tool-io tool-args\"><summary>Parameters</summary>")
                .Append("<table class=\"tool-args-table\"><tbody>");
            foreach (var prop in arguments.Properties())
            {
                sb.Append("<tr><td class=\"ta-name\">").Append(EncodeHtml(prop.Name)).Append("</td><td class=\"ta-val\">");
                if (prop.Value is JObject obj)
                {
                    sb.Append("<details class=\"tool-io nested\"><summary>{ ")
                        .Append(obj.Count).Append(obj.Count == 1 ? " key" : " keys")
                        .Append(" }</summary><pre><code>");
                    AppendJsonHtml(sb, obj, 0);
                    sb.Append("</code></pre></details>");
                }
                else if (prop.Value is JArray arr)
                {
                    sb.Append("<details class=\"tool-io nested\"><summary>[ ")
                        .Append(arr.Count).Append(arr.Count == 1 ? " item" : " items")
                        .Append(" ]</summary><pre><code>");
                    AppendJsonHtml(sb, arr, 0);
                    sb.Append("</code></pre></details>");
                }
                else
                {
                    sb.Append("<code>").Append(EncodeHtml(JsonLiteral(prop.Value))).Append("</code>");
                }

                sb.Append("</td></tr>");
            }

            sb.Append("</tbody></table></details>");
        }

        /// <summary>
        /// Renders a JSON document as indented HTML with syntax-coloring spans
        /// (<c>jt-key</c>/<c>jt-str</c>/<c>jt-num</c>/<c>jt-bool</c>/<c>jt-null</c>)
        /// so the result block reads as formatted JSON instead of a text dump.
        /// All literals are HTML-encoded before embedding.
        /// </summary>
        internal static void AppendJsonHtml(StringBuilder sb, JToken token, int indent)
        {
            switch (token)
            {
                case JObject obj:
                {
                    sb.Append('{');
                    if (obj.Count > 0)
                    {
                        sb.Append('\n');
                        var i = 0;
                        foreach (var prop in obj.Properties())
                        {
                            AppendIndent(sb, indent + 1);
                            sb.Append("<span class=\"jt-key\">")
                                .Append(EncodeHtml(JsonConvert.SerializeObject(prop.Name)))
                                .Append("</span>: ");
                            AppendJsonHtml(sb, prop.Value, indent + 1);
                            if (++i < obj.Count)
                            {
                                sb.Append(',');
                            }

                            sb.Append('\n');
                        }

                        AppendIndent(sb, indent);
                        sb.Append('}');
                    }
                    else
                    {
                        sb.Append('}');
                    }

                    break;
                }

                case JArray arr:
                {
                    sb.Append('[');
                    if (arr.Count > 0)
                    {
                        sb.Append('\n');
                        for (var i = 0; i < arr.Count; i++)
                        {
                            AppendIndent(sb, indent + 1);
                            AppendJsonHtml(sb, arr[i], indent + 1);
                            if (i + 1 < arr.Count)
                            {
                                sb.Append(',');
                            }

                            sb.Append('\n');
                        }

                        AppendIndent(sb, indent);
                        sb.Append(']');
                    }
                    else
                    {
                        sb.Append(']');
                    }

                    break;
                }

                default:
                {
                    var css = token.Type switch
                    {
                        JTokenType.String or JTokenType.Date or JTokenType.Guid or JTokenType.Uri
                            or JTokenType.TimeSpan or JTokenType.Bytes => "jt-str",
                        JTokenType.Integer or JTokenType.Float => "jt-num",
                        JTokenType.Boolean => "jt-bool",
                        JTokenType.Null or JTokenType.Undefined => "jt-null",
                        _ => null,
                    };
                    var literal = EncodeHtml(JsonLiteral(token));
                    if (css == null)
                    {
                        sb.Append(literal);
                    }
                    else
                    {
                        sb.Append("<span class=\"").Append(css).Append("\">").Append(literal).Append("</span>");
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// JSON literal text for a scalar token (strings keep their quotes).
        /// Complex tokens fall back to their compact JSON form.
        /// </summary>
        internal static string JsonLiteral(JToken token)
        {
            if (token == null)
            {
                return "null";
            }

            return token is JValue ? JsonConvert.SerializeObject(token) : token.ToString(Formatting.None);
        }

        /// <summary>
        /// Short inline value for the digest line: strings are quoted and capped
        /// at 40 characters; objects/arrays collapse to a size placeholder.
        /// </summary>
        private static string DigestValue(JToken token)
        {
            switch (token)
            {
                case JObject obj:
                    return "{" + obj.Count + (obj.Count == 1 ? " key" : " keys") + "}";
                case JArray arr:
                    return "[" + arr.Count + (arr.Count == 1 ? " item" : " items") + "]";
                case JValue v when v.Type == JTokenType.String:
                {
                    var s = v.Value<string>() ?? string.Empty;
                    if (s.Length > 40)
                    {
                        s = s.Substring(0, 40) + "…";
                    }

                    return JsonConvert.SerializeObject(s);
                }

                default:
                    return JsonLiteral(token);
            }
        }

        /// <summary>
        /// HTML-encodes a fragment for safe embedding in the card markup. Argument
        /// names and values are model-controlled text — encoding is the injection
        /// boundary between tool payloads and the chat DOM.
        /// </summary>
        internal static string EncodeHtml(string text)
        {
            return WebUtility.HtmlEncode(text ?? string.Empty);
        }

        private static void AppendIndent(StringBuilder sb, int indent)
        {
            for (var i = 0; i < indent; i++)
            {
                sb.Append("  ");
            }
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
