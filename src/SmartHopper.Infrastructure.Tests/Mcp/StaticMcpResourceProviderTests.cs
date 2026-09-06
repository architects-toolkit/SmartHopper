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
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.Mcp;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="StaticMcpResourceProvider"/>. No Rhino/Grasshopper dependencies.
    /// </summary>
    public class StaticMcpResourceProviderTests
    {
        [Fact]
        public async Task ListResourcesAsync_ReturnsFourStaticResources()
        {
            var provider = new StaticMcpResourceProvider((_, __, ___) => Task.FromResult<JObject?>(null));

            var resources = await provider.ListResourcesAsync();

            Assert.Equal(4, resources.Count);
            Assert.Contains(resources, r => r.Uri.ToString() == "docs:///ghjson-schema");
            Assert.Contains(resources, r => r.Uri.ToString() == "docs:///smarthopper-readme");
            Assert.Contains(resources, r => r.Uri.ToString() == "docs:///smarthopper-workflows");
            Assert.Contains(resources, r => r.Uri.ToString() == "docs:///tool-help");
        }

        [Fact]
        public async Task GetResourceAsync_GhJsonSchema_ReturnsEmbeddedSpec()
        {
            var provider = new StaticMcpResourceProvider((_, __, ___) => Task.FromResult<JObject?>(null));

            var resource = await provider.GetResourceAsync(new Uri("docs:///ghjson-schema", UriKind.Absolute));

            Assert.NotNull(resource);
            var text = await resource!.GetTextAsync();
            Assert.Contains("GhJSON", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetResourceAsync_SmarthopperReadme_ReturnsEmbeddedReadme()
        {
            var provider = new StaticMcpResourceProvider((_, __, ___) => Task.FromResult<JObject?>(null));

            var resource = await provider.GetResourceAsync(new Uri("docs:///smarthopper-readme", UriKind.Absolute));

            Assert.NotNull(resource);
            var text = await resource!.GetTextAsync();
            Assert.Contains("Canvas state reading", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetResourceAsync_SmarthopperWorkflows_ReturnsEmbeddedWorkflows()
        {
            var provider = new StaticMcpResourceProvider((_, __, ___) => Task.FromResult<JObject?>(null));

            var resource = await provider.GetResourceAsync(new Uri("docs:///smarthopper-workflows", UriKind.Absolute));

            Assert.NotNull(resource);
            var text = await resource!.GetTextAsync();
            Assert.Contains("inspect_canvas", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetResourceAsync_ToolHelp_CallsExecutor()
        {
            var called = false;
            var provider = new StaticMcpResourceProvider((name, args, _) =>
            {
                called = true;
                Assert.Equal("smarthopper_tool_help", name);
                return Task.FromResult<JObject?>(new JObject
                {
                    ["found"] = true,
                    ["category"] = "Test",
                    ["description"] = "A test tool.",
                    ["input_schema"] = new JObject(),
                    ["output_schema"] = new JObject(),
                });
            });

            var resource = await provider.GetResourceAsync(new Uri("docs:///tool-help/gh_get", UriKind.Absolute));

            Assert.NotNull(resource);
            var text = await resource!.GetTextAsync();
            Assert.True(called);
            Assert.Contains("gh_get", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("A test tool.", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetResourceAsync_UnknownResource_ReturnsNull()
        {
            var provider = new StaticMcpResourceProvider((_, __, ___) => Task.FromResult<JObject?>(null));

            var resource = await provider.GetResourceAsync(new Uri("docs:///unknown", UriKind.Absolute));

            Assert.Null(resource);
        }
    }
}
