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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.Infrastructure.Mcp;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="JsonRpcDispatcher"/>. No Rhino/Grasshopper dependencies.
    /// </summary>
    public class JsonRpcDispatcherTests
    {
        private const string ReadOnlySchema = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}";

        [Fact]
        public async Task Dispatch_Initialize_ReturnsServerInfoAndProtocolVersion()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions
            {
                ServerName = "smarthopper",
                ServerVersion = "9.9.9",
            });

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}");

            Assert.NotNull(raw);
            var obj = JObject.Parse(raw!);
            Assert.Equal("2.0", (string?)obj["jsonrpc"]);
            Assert.Equal(1, (int?)obj["id"]);
            Assert.NotNull(obj["result"]);
            Assert.Equal("smarthopper", (string?)obj["result"]?["serverInfo"]?["name"]);
            Assert.Equal("9.9.9", (string?)obj["result"]?["serverInfo"]?["version"]);
            Assert.NotNull((string?)obj["result"]?["protocolVersion"]);
            Assert.NotNull(obj["result"]?["capabilities"]?["tools"]);
        }

        [Fact]
        public async Task Dispatch_ToolsList_ReturnsExposedToolsOnly()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                ("gh_get", ReadOnlySchema, false),
                ("gh_put", ReadOnlySchema, true));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");

            var obj = JObject.Parse(raw!);
            var tools = (JArray?)obj["result"]?["tools"];
            Assert.NotNull(tools);
            Assert.Single(tools!);
            Assert.Equal("gh_get", (string?)tools![0]["name"]);
            Assert.Equal("object", (string?)tools[0]["inputSchema"]?["type"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_ReturnsTextContentWithToolResult()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        Request = call,
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["ok"] = true, ["echoed"] = call.GetToolCall().Arguments },
                    }));
                    return Task.FromResult(ret);
                },
                tools: ("gh_get", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{\"path\":\"/foo\"}}}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(false, (bool?)obj["result"]?["isError"]);
            var text = (string?)obj["result"]?["content"]?[0]?["text"];
            Assert.NotNull(text);
            var payload = JObject.Parse(text!);
            Assert.Equal(true, (bool?)payload["ok"]);
            Assert.Equal("/foo", (string?)payload["echoed"]?["path"]);
        }

        [Fact]
        public async Task Dispatch_UnknownMethod_ReturnsMethodNotFoundError()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"unknown/method\"}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(-32601, (int?)obj["error"]?["code"]);
        }

        [Fact]
        public async Task Dispatch_Initialize_IncludesResourceAndPromptCapabilities()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}");

            var obj = JObject.Parse(raw!);
            Assert.NotNull(obj["result"]?["capabilities"]?["resources"]);
            Assert.NotNull(obj["result"]?["capabilities"]?["prompts"]);
        }

        [Fact]
        public async Task Dispatch_ResourcesList_ListsStaticResources()
        {
            var dispatcher = BuildDispatcherWithProviders(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"resources/list\"}");

            var obj = JObject.Parse(raw!);
            var resources = (JArray?)obj["result"]?["resources"];
            Assert.NotNull(resources);
            Assert.Single(resources!);
            Assert.Equal("docs:///test-doc", (string?)resources![0]["uri"]);
        }

        [Fact]
        public async Task Dispatch_ResourcesRead_ReturnsResourceText()
        {
            var dispatcher = BuildDispatcherWithProviders(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"resources/read\",\"params\":{\"uri\":\"docs:///test-doc\"}}");

            var obj = JObject.Parse(raw!);
            var contents = (JArray?)obj["result"]?["contents"];
            Assert.NotNull(contents);
            Assert.Single(contents!);
            Assert.Equal("docs:///test-doc", (string?)contents![0]["uri"]);
            Assert.Equal("text/markdown", (string?)contents[0]["mimeType"]);
            Assert.Equal("Test resource text", (string?)contents[0]["text"]);
        }

        [Fact]
        public async Task Dispatch_PromptsList_ListsStaticPrompts()
        {
            var dispatcher = BuildDispatcherWithProviders(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"prompts/list\"}");

            var obj = JObject.Parse(raw!);
            var prompts = (JArray?)obj["result"]?["prompts"];
            Assert.NotNull(prompts);
            Assert.NotEmpty(prompts!);
            Assert.Contains(prompts!, p => (string?)p["name"] == "test-prompt");
        }

        [Fact]
        public async Task Dispatch_PromptsGet_ReturnsMessages()
        {
            var dispatcher = BuildDispatcherWithProviders(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"prompts/get\",\"params\":{\"name\":\"test-prompt\"}}");

            var obj = JObject.Parse(raw!);
            Assert.Equal("A test prompt", (string?)obj["result"]?["description"]);
            var messages = (JArray?)obj["result"]?["messages"];
            Assert.NotNull(messages);
            Assert.Single(messages!);
            Assert.Equal("user", (string?)messages![0]["role"]);
            Assert.Equal("text", (string?)messages[0]["content"]?["type"]);
            Assert.Equal("Hello", (string?)messages[0]["content"]?["text"]);
        }

        [Fact]
        public async Task Dispatch_InvalidJson_ReturnsParseError()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync("not-json");

            var obj = JObject.Parse(raw!);
            Assert.Equal(-32700, (int?)obj["error"]?["code"]);
        }

        [Fact]
        public async Task Dispatch_Notification_ReturnsNoResponse()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

            Assert.Null(raw);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_MissingName_ReturnsToolError()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{}}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(true, (bool?)obj["result"]?["isError"]);
            Assert.Contains("name", (string?)obj["result"]?["content"]?[0]?["text"]);
        }

        private static JsonRpcDispatcher BuildDispatcher(
            McpServerOptions options,
            params (string name, string schema, bool mutatesCanvas)[] tools)
        {
            return BuildDispatcher(options, _ => Task.FromResult(new AIReturn()), tools);
        }

        private static JsonRpcDispatcher BuildDispatcher(
            McpServerOptions options,
            System.Func<AIToolCall, Task<AIReturn>> executor,
            params (string name, string schema, bool mutatesCanvas)[] tools)
        {
            var catalog = new Dictionary<string, AITool>();
            foreach (var (name, schema, mutatesCanvas) in tools)
            {
                catalog[name] = new AITool(
                    name: name,
                    description: $"Test tool {name}",
                    category: "Test",
                    parametersSchema: schema,
                    execute: _ => Task.FromResult(new AIReturn()),
                    mutatesCanvas: mutatesCanvas);
            }

            var adapter = new AIToolMcpAdapter(options, () => catalog, executor);
            return new JsonRpcDispatcher(options, adapter);
        }

        private static JsonRpcDispatcher BuildDispatcherWithProviders(McpServerOptions options)
        {
            var catalog = new Dictionary<string, AITool>();
            var adapter = new AIToolMcpAdapter(options, () => catalog, _ => Task.FromResult(new AIReturn()));

            var resource = new McpResource(
                new Uri("docs:///test-doc", UriKind.Absolute),
                "Test Doc",
                "text/markdown",
                "A test resource",
                _ => Task.FromResult("Test resource text"));

            var resourceProvider = new TestResourceProvider(new[] { resource });

            var prompt = new McpPrompt(
                new Uri("prompts:///test-prompt", UriKind.Absolute),
                "test-prompt",
                "A test prompt",
                _ => Task.FromResult<IReadOnlyList<McpPromptMessage>>(
                    new List<McpPromptMessage> { new McpPromptMessage("user", "text", "Hello") }));

            var promptProvider = new TestPromptProvider(new[] { prompt });

            return new JsonRpcDispatcher(options, adapter, resourceProvider, promptProvider);
        }

        private sealed class TestResourceProvider : IMcpResourceProvider
        {
            private readonly IReadOnlyList<McpResource> resources;

            public TestResourceProvider(IReadOnlyList<McpResource> resources)
            {
                this.resources = resources;
            }

            public Task<IReadOnlyList<McpResource>> ListResourcesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.resources);
            }

            public Task<McpResource?> GetResourceAsync(Uri uri, CancellationToken cancellationToken = default)
            {
                var match = this.resources.FirstOrDefault(r =>
                    string.Equals(r.Uri.ToString(), uri.ToString(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(match);
            }
        }

        private sealed class TestPromptProvider : IMcpPromptProvider
        {
            private readonly IReadOnlyList<McpPrompt> prompts;

            public TestPromptProvider(IReadOnlyList<McpPrompt> prompts)
            {
                this.prompts = prompts;
            }

            public Task<IReadOnlyList<McpPrompt>> ListPromptsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.prompts);
            }

            public Task<McpPrompt?> GetPromptAsync(Uri uri, CancellationToken cancellationToken = default)
            {
                var match = this.prompts.FirstOrDefault(p =>
                    string.Equals(p.Uri.ToString(), uri.ToString(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(match);
            }
        }
    }
}
