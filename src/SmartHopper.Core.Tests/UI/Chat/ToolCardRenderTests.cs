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

namespace SmartHopper.Core.Tests.UI.Chat
{
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.UI.Chat;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using Xunit;

    /// <summary>
    /// End-to-end tests for tool-card rendering: <see cref="ChatResourceManager"/>
    /// runs the interaction markup through Markdig, so these tests verify the
    /// structured HTML (details/table/copy button) actually survives the pipeline.
    /// </summary>
    public class ToolCardRenderTests
    {
        [Fact(DisplayName = "Tool call card renders digest and parameter details")]
        public void RenderInteraction_ToolCall_RendersStructuredCard()
        {
            var renderer = new HtmlChatRenderer();
            var call = new AIInteractionToolCall
            {
                Name = "gh_put",
                Arguments = new JObject
                {
                    ["a"] = 1,
                    ["nested"] = new JObject { ["inner"] = "v" },
                },
            };

            var html = renderer.RenderInteraction(call);

            Assert.Contains("tool-call-line", html);
            Assert.Contains("gh_put(a: 1", html);
            Assert.Contains("tool-args-table", html);
            Assert.Contains("message tool", html);
            Assert.Contains("data-state=\"pending\"", html);
        }

        [Fact(DisplayName = "Tool call with no arguments renders header only")]
        public void RenderInteraction_ToolCall_NoArgs_EmitsNoBody()
        {
            var renderer = new HtmlChatRenderer();
            var call = new AIInteractionToolCall { Name = "gh_report" };

            var html = renderer.RenderInteraction(call);

            Assert.DoesNotContain("tool-call-line", html);
            Assert.DoesNotContain("tool-args-table", html);
        }

        [Fact(DisplayName = "Tool result card renders result details and copy-raw button")]
        public void RenderInteraction_ToolResult_RendersResultBlock()
        {
            var renderer = new HtmlChatRenderer();
            var result = new AIInteractionToolResult
            {
                Name = "gh_report",
                Result = new JObject { ["report"] = "# status", ["count"] = 3 },
            };

            var html = renderer.RenderInteraction(result);

            Assert.Contains("gh_report", html);
            Assert.Contains("→ done", html);
            Assert.Contains("tool-result", html);
            Assert.Contains("cui-copy-raw", html);
            Assert.Contains("data-copy=", html);
            Assert.Contains("jt-key", html);
            Assert.Contains("data-state=\"done\"", html);
        }

        [Fact(DisplayName = "Tool result failure surfaces the error callout")]
        public void RenderInteraction_ToolResult_Error_Callout()
        {
            var renderer = new HtmlChatRenderer();
            var result = new AIInteractionToolResult
            {
                Name = "gh_put",
                Result = new JObject { ["error"] = "rejected by the user" },
            };

            var html = renderer.RenderInteraction(result);

            Assert.Contains("→ failed", html);
            Assert.Contains("tool-error", html);
            Assert.Contains("rejected by the user", html);
            Assert.Contains("data-state=\"error\"", html);
        }

        [Fact(DisplayName = "Tool card copy source stays plain text, not markup")]
        public void RenderInteraction_ToolResult_CopyContentIsReadable()
        {
            var renderer = new HtmlChatRenderer();
            var result = new AIInteractionToolResult
            {
                Name = "gh_report",
                Arguments = new JObject { ["a"] = 1 },
                Result = new JObject { ["ok"] = true },
            };

            var html = renderer.RenderInteraction(result);

            // data-copy-content feeds the whole-message copy handler — it must
            // carry the readable args → result text, not the card markup.
            var start = html.IndexOf("data-copy-content='", System.StringComparison.Ordinal)
                + "data-copy-content='".Length;
            var end = html.IndexOf('\'', start);
            var copySource = html.Substring(start, end - start);
            Assert.DoesNotContain("details", copySource);
            Assert.Contains("``{&quot;a&quot;:1}``", copySource);
        }
    }
}
