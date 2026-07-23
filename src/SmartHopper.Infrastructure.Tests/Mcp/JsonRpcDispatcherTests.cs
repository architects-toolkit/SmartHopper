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
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.Infrastructure.Mcp;
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
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"resources/list\"}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(-32601, (int?)obj["error"]?["code"]);
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
    }
}
