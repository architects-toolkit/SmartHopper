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

namespace SmartHopper.Infrastructure.Tests.Mcp
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.Mcp;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="StaticMcpPromptProvider"/>. No Rhino/Grasshopper dependencies.
    /// </summary>
    public class StaticMcpPromptProviderTests
    {
        [Fact]
        public async Task ListPromptsAsync_ReturnsThreeStaticPrompts()
        {
            var provider = new StaticMcpPromptProvider();

            var prompts = await provider.ListPromptsAsync();

            Assert.Equal(3, prompts.Count);
            Assert.Contains(prompts, p => p.Name == "grasshopper-expert");
            Assert.Contains(prompts, p => p.Name == "script-writer");
            Assert.Contains(prompts, p => p.Name == "canvas-debugger");
        }

        [Fact]
        public async Task GetPromptAsync_ExistingPrompt_ReturnsStaticMessages()
        {
            var provider = new StaticMcpPromptProvider();

            var prompt = await provider.GetPromptAsync(new Uri("prompts:///grasshopper-expert", UriKind.Absolute));

            Assert.NotNull(prompt);
            Assert.Equal("grasshopper-expert", prompt!.Name);

            var messages = await prompt.GetMessagesAsync();
            Assert.Single(messages);
            var text = messages[0].Text;
            Assert.Contains("expert in Grasshopper", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("docs:///smarthopper-readme", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptAsync_CanvasDebuggerPrompt_ReferencesResource()
        {
            var provider = new StaticMcpPromptProvider();

            var prompt = await provider.GetPromptAsync(new Uri("prompts:///canvas-debugger", UriKind.Absolute));

            Assert.NotNull(prompt);

            var messages = await prompt!.GetMessagesAsync();
            var text = messages[0].Text;
            Assert.Contains("docs:///smarthopper-workflows", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gh_report", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptAsync_UnknownPrompt_ReturnsNull()
        {
            var provider = new StaticMcpPromptProvider();

            var prompt = await provider.GetPromptAsync(new Uri("prompts:///unknown", UriKind.Absolute));

            Assert.Null(prompt);
        }
    }
}
