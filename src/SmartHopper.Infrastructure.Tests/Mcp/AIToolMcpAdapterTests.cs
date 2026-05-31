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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.Infrastructure.Mcp;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="AIToolMcpAdapter"/>. No Rhino/Grasshopper dependencies.
    /// </summary>
    public class AIToolMcpAdapterTests
    {
        private const string ReadOnlySchema = "{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\"}}}";

        [Fact]
        public void BuildDescriptors_OmitsMutatingToolsByDefault()
        {
            var tools = BuildCatalog(
                ("gh_get", ReadOnlySchema),
                ("gh_put", ReadOnlySchema));
            var adapter = new AIToolMcpAdapter(new McpServerOptions(), () => tools, _ => Task.FromResult(new AIReturn()));

            var descriptors = adapter.BuildDescriptors();

            Assert.Single(descriptors);
            Assert.Equal("gh_get", descriptors[0].Name);
        }

        [Fact]
        public void BuildDescriptors_IncludesMutatingToolsWhenOptedIn()
        {
            var tools = BuildCatalog(
                ("gh_get", ReadOnlySchema),
                ("gh_put", ReadOnlySchema));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                _ => Task.FromResult(new AIReturn()));

            var descriptors = adapter.BuildDescriptors();

            Assert.Equal(2, descriptors.Count);
            Assert.Contains(descriptors, d => d.Name == "gh_get");
            Assert.Contains(descriptors, d => d.Name == "gh_put");
        }

        [Fact]
        public void BuildDescriptors_AllowListNarrowsExposedTools()
        {
            var tools = BuildCatalog(
                ("gh_get", ReadOnlySchema),
                ("script_review", ReadOnlySchema),
                ("script_generate", ReadOnlySchema));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { EnabledTools = new[] { "gh_get" } },
                () => tools,
                _ => Task.FromResult(new AIReturn()));

            var descriptors = adapter.BuildDescriptors();

            Assert.Single(descriptors);
            Assert.Equal("gh_get", descriptors[0].Name);
        }

        [Fact]
        public void BuildDescriptors_AllowListOverridesMutatingFilter()
        {
            var tools = BuildCatalog(("gh_put", ReadOnlySchema));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { EnabledTools = new[] { "gh_put" } },
                () => tools,
                _ => Task.FromResult(new AIReturn()));

            var descriptors = adapter.BuildDescriptors();

            Assert.Single(descriptors);
            Assert.Equal("gh_put", descriptors[0].Name);
        }

        [Fact]
        public void BuildDescriptors_ParsesParametersSchemaIntoJObject()
        {
            var tools = BuildCatalog(("gh_get", ReadOnlySchema));
            var adapter = new AIToolMcpAdapter(new McpServerOptions(), () => tools, _ => Task.FromResult(new AIReturn()));

            var descriptors = adapter.BuildDescriptors();

            Assert.Single(descriptors);
            Assert.Equal("object", (string?)descriptors[0].InputSchema["type"]);
            Assert.Equal("string", (string?)descriptors[0].InputSchema["properties"]?["q"]?["type"]);
        }

        [Fact]
        public void BuildDescriptors_BadSchemaFallsBackToObject()
        {
            var tools = BuildCatalog(("gh_get", "not-json"));
            var adapter = new AIToolMcpAdapter(new McpServerOptions(), () => tools, _ => Task.FromResult(new AIReturn()));

            var descriptors = adapter.BuildDescriptors();

            Assert.Equal("object", (string?)descriptors[0].InputSchema["type"]);
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsErrorForUnknownTool()
        {
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions(),
                () => new Dictionary<string, AITool>(),
                _ => Task.FromResult(new AIReturn()));

            var result = await adapter.ExecuteAsync("missing", new JObject());

            Assert.True(result.IsError);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("missing", result.ErrorMessage!);
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsErrorWhenToolHidden()
        {
            var tools = BuildCatalog(("gh_put", ReadOnlySchema));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions(),
                () => tools,
                _ => Task.FromResult(new AIReturn()));

            var result = await adapter.ExecuteAsync("gh_put", new JObject());

            Assert.True(result.IsError);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("not exposed", result.ErrorMessage!);
        }

        [Fact]
        public async Task ExecuteAsync_PassesArgumentsToExecutorAndReturnsToolResult()
        {
            var tools = BuildCatalog(("gh_get", ReadOnlySchema));

            JObject? observedArgs = null;
            Task<AIReturn> Executor(AIToolCall call)
            {
                var pending = call.Body.PendingToolCallsList();
                observedArgs = pending.Count > 0 ? pending[0].Arguments : null;

                var ret = new AIReturn
                {
                    Request = call,
                    SkipRequestValidation = true,
                    SkipMetricsValidation = true,
                };
                var ok = new AIInteractionToolResult
                {
                    Name = call.GetToolCall().Name,
                    Result = new JObject { ["echoed"] = (JObject?)observedArgs?.DeepClone() ?? new JObject() },
                };
                ret.SetBody(AIBody.Empty.WithAppended(ok));
                return Task.FromResult(ret);
            }

            var adapter = new AIToolMcpAdapter(new McpServerOptions(), () => tools, Executor);

            var args = new JObject { ["q"] = "hello" };
            var result = await adapter.ExecuteAsync("gh_get", args);

            Assert.False(result.IsError);
            Assert.NotNull(observedArgs);
            Assert.Equal("hello", (string?)observedArgs!["q"]);
            Assert.Equal("hello", (string?)result.Payload["echoed"]?["q"]);
        }

        [Fact]
        public async Task ExecuteAsync_ConvertsToolErrorIntoErrorResult()
        {
            var tools = BuildCatalog(("gh_get", ReadOnlySchema));
            Task<AIReturn> Executor(AIToolCall call)
            {
                var ret = new AIReturn
                {
                    Request = call,
                    SkipRequestValidation = true,
                    SkipMetricsValidation = true,
                };
                ret.CreateToolError("boom", call);
                return Task.FromResult(ret);
            }

            var adapter = new AIToolMcpAdapter(new McpServerOptions(), () => tools, Executor);
            var result = await adapter.ExecuteAsync("gh_get", new JObject());

            Assert.True(result.IsError);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("boom", result.ErrorMessage!);
        }

        private static IReadOnlyDictionary<string, AITool> BuildCatalog(params (string name, string schema)[] entries)
        {
            var dict = new Dictionary<string, AITool>();
            foreach (var (name, schema) in entries)
            {
                dict[name] = new AITool(
                    name: name,
                    description: $"Test tool {name}",
                    category: "Test",
                    parametersSchema: schema,
                    execute: _ => Task.FromResult(new AIReturn()));
            }

            return dict;
        }
    }
}
