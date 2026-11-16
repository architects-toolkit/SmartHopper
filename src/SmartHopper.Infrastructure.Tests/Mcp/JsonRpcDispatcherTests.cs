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
    using SmartHopper.ProviderSdk.AICall.Metrics;
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
        public async Task Dispatch_Initialize_IncludesHostDocumentInstructions()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions());

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}");

            var obj = JObject.Parse(raw!);
            var instructions = (string?)obj["result"]?["instructions"];
            Assert.NotNull(instructions);
            Assert.Contains("document", instructions);
            Assert.Contains("gh_document_list", instructions);
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
        public async Task Dispatch_ToolsCall_ImagePayload_ReturnsImageContentBlock()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject
                        {
                            ["imageBase64"] = "QUJD",
                            ["mimeType"] = "image/png",
                            ["width"] = 4,
                            ["height"] = 4,
                        },
                    }));
                    return Task.FromResult(ret);
                },
                tools: ("canvas_screenshot", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"canvas_screenshot\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(false, (bool?)obj["result"]?["isError"]);
            var content = (JArray?)obj["result"]?["content"];
            Assert.NotNull(content);
            Assert.Equal("image", (string?)content![0]?["type"]);
            Assert.Equal("QUJD", (string?)content[0]?["data"]);
            Assert.Equal("image/png", (string?)content[0]?["mimeType"]);
            Assert.Equal("text", (string?)content[1]?["type"]);
            var metadata = JObject.Parse((string?)content[1]!["text"]!);
            Assert.Equal(4, (int?)metadata["width"]);
            Assert.Null(metadata["imageBase64"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_JsonPayload_StaysTextOnly()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["ok"] = true },
                    }));
                    return Task.FromResult(ret);
                },
                tools: ("gh_get", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            var content = (JArray?)obj["result"]?["content"];
            Assert.NotNull(content);
            Assert.Single(content!);
            Assert.Equal("text", (string?)content[0]?["type"]);
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
            Assert.Equal("text/markdown", (string?)contents![0]["mimeType"]);
            Assert.Equal("Test resource text", (string?)contents![0]["text"]);
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
            Assert.Equal("text", (string?)messages![0]["content"]?["type"]);
            Assert.Equal("Hello", (string?)messages![0]["content"]?["text"]);
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

        [Fact]
        public async Task Dispatch_ToolsCall_TypedImages_EmitNativeBlocksInOrder_WithStructuredContent()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject
                        {
                            ["content"] = "# doc",
                            ["consumerCapability"] = "supported",
                        },
                        Images = new List<ToolResultImage>
                        {
                            new ToolResultImage
                            {
                                ImageData = "QUJD",
                                MimeType = "image/png",
                                SendToModel = true,
                                SourceReference = new SourceContentReference { DocumentId = "doc", SourceName = "a.pdf", AssetId = "img-1", Kind = SourceContentKind.Image },
                            },
                            new ToolResultImage
                            {
                                ImageData = "REVG",
                                MimeType = "image/png",
                                SendToModel = true,
                                SourceReference = new SourceContentReference { DocumentId = "doc", SourceName = "a.pdf", AssetId = "img-2", Kind = SourceContentKind.Image },
                            },
                            new ToolResultImage
                            {
                                ImageData = "R0hJ",
                                MimeType = "image/png",
                                SendToModel = false,
                                SourceReference = new SourceContentReference { DocumentId = "doc", SourceName = "a.pdf", AssetId = "img-3", Kind = SourceContentKind.Image },
                            },
                        },
                    }));
                    return Task.FromResult(ret);
                },
                tools: ("file2md", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"file2md\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            var content = (JArray?)obj["result"]?["content"];
            Assert.NotNull(content);

            // Exactly two native image blocks, in order, before the text block.
            var images = content!.Where(b => (string?)b["type"] == "image").ToList();
            Assert.Equal(2, images.Count);
            Assert.Equal("QUJD", (string?)images[0]["data"]);
            Assert.Equal("REVG", (string?)images[1]["data"]);
            Assert.True((string?)content[0]?["type"] == "image" && (string?)content[1]?["type"] == "image");
            Assert.Equal("text", (string?)content[2]?["type"]);

            // No base64 in text or structuredContent; nativeImageOrder correlates IDs.
            var text = (string?)content[2]!["text"];
            Assert.DoesNotContain("QUJD", text);
            var structured = (JObject?)obj["result"]?["structuredContent"];
            Assert.NotNull(structured);
            Assert.DoesNotContain("QUJD", structured!.ToString(Newtonsoft.Json.Formatting.None));
            var order = (JArray?)structured["nativeImageOrder"];
            Assert.NotNull(order);
            Assert.Equal(new[] { "img-1", "img-2" }, order!.Values<string>().ToArray());
        }

        [Fact]
        public async Task Dispatch_ToolsCall_AdaptedImages_EmitNoNativeBlocks()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject
                        {
                            ["content"] = "# doc",
                            ["imageEvidence"] = new JArray(new JObject { ["evidence"] = "desc" }),
                            ["consumerCapability"] = "unsupported",
                        },
                        Images = new List<ToolResultImage>
                        {
                            new ToolResultImage
                            {
                                ImageData = "QUJD",
                                MimeType = "image/png",
                                SendToModel = false,
                                SourceReference = new SourceContentReference { DocumentId = "doc", SourceName = "a.pdf", AssetId = "img-1", Kind = SourceContentKind.Image },
                            },
                        },
                    }));
                    return Task.FromResult(ret);
                },
                tools: ("file2md", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"file2md\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            var content = (JArray?)obj["result"]?["content"];
            Assert.NotNull(content);
            Assert.DoesNotContain(content!, b => (string?)b["type"] == "image");

            var structured = (JObject?)obj["result"]?["structuredContent"];
            Assert.NotNull(structured);
            Assert.Equal("unsupported", (string?)structured!["consumerCapability"]);
            Assert.NotNull(structured["imageEvidence"]);
            Assert.Null(structured["nativeImageOrder"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_LegacyScreenshot_StructuredContentStripsBinary()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject
                        {
                            ["imageBase64"] = "QUJD",
                            ["mimeType"] = "image/png",
                            ["width"] = 4,
                        },
                    }));
                    return Task.FromResult(ret);
                },
                tools: ("canvas_screenshot", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"canvas_screenshot\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            var structured = (JObject?)obj["result"]?["structuredContent"];
            Assert.NotNull(structured);
            Assert.Equal(4, (int?)structured!["width"]);
            Assert.Null(structured["imageBase64"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_HelperUsage_Meta_OnSuccessAndError()
        {
            var metric = new AIMetrics { Provider = "helper-fake", Model = "vision", InputTokensPrompt = 12, OutputTokensGeneration = 7 };

            var success = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["content"] = "# ok" },
                    }));
                    ret.ExtraMetrics.Add(metric);
                    return Task.FromResult(ret);
                },
                tools: ("file2md", ReadOnlySchema, false));

            var rawOk = await success.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"tools/call\",\"params\":{\"name\":\"file2md\",\"arguments\":{}}}");
            var objOk = JObject.Parse(rawOk!);
            var usageOk = (JArray?)objOk["result"]?["_meta"]?["smarthopper/helperUsage"];
            Assert.NotNull(usageOk);
            var decodedOk = Assert.IsType<AIMetrics>(Assert.Single(usageOk!).ToObject<AIMetrics>());
            Assert.Equal("helper-fake", decodedOk.Provider);
            Assert.Equal("vision", decodedOk.Model);
            Assert.Equal(12, decodedOk.InputTokensPrompt);
            Assert.Equal(7, decodedOk.OutputTokensGeneration);
            var textOk = ((JArray?)objOk["result"]?["content"])?.Select(b => (string?)b["text"]).FirstOrDefault(t => t != null);
            Assert.DoesNotContain("helperUsage", textOk ?? string.Empty);

            var failure = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.CreateError("boom");
                    ret.ExtraMetrics.Add(metric);
                    return Task.FromResult(ret);
                },
                tools: ("file2md", ReadOnlySchema, false));

            var rawErr = await failure.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"file2md\",\"arguments\":{}}}");
            var objErr = JObject.Parse(rawErr!);
            var usageErr = (JArray?)objErr["result"]?["_meta"]?["smarthopper/helperUsage"];
            Assert.NotNull(usageErr);
            var decodedErr = Assert.IsType<AIMetrics>(Assert.Single(usageErr!).ToObject<AIMetrics>());
            Assert.Equal("helper-fake", decodedErr.Provider);
            Assert.Equal("vision", decodedErr.Model);
            Assert.Equal(12, decodedErr.InputTokensPrompt);
            Assert.Equal(7, decodedErr.OutputTokensGeneration);
        }

        [Fact]
        public void Envelope_UnknownConsumer_StaysUnknown()
        {
            var envelope = JsonRpcDispatcher.BuildToolCallEnvelope(McpToolCallResult.Ok(
                new JObject { ["content"] = "# doc", ["consumerCapability"] = "unknown" }));
            var structured = (JObject?)envelope["structuredContent"];
            Assert.NotNull(structured);
            Assert.Equal("unknown", (string?)structured!["consumerCapability"]);
        }

        [Fact]
        public void Envelope_ScalarOrArrayPayload_WrapsUnderResult()
        {
            var array = JsonRpcDispatcher.BuildToolCallEnvelope(
                McpToolCallResult.Ok(new JArray("a", "b")));
            var structuredArray = (JObject?)array["structuredContent"];
            Assert.Equal("a", (string?)structuredArray?["result"]?[0]);
            Assert.Equal("b", (string?)structuredArray?["result"]?[1]);

            var scalar = JsonRpcDispatcher.BuildToolCallEnvelope(
                McpToolCallResult.Ok(new JValue("plain")));
            var structuredScalar = (JObject?)scalar["structuredContent"];
            Assert.Equal("plain", (string?)structuredScalar?["result"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_SuccessWithWarning_ForwardsDiagnosticsToContentAndMeta()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["ok"] = true },
                    }));
                    ret.AddRuntimeMessage(
                        SmartHopper.ProviderSdk.Diagnostics.SHRuntimeMessageSeverity.Warning,
                        SmartHopper.ProviderSdk.Diagnostics.SHRuntimeMessageOrigin.Tool,
                        "Unrecognized argument(s) 'bogus' were ignored.");
                    return Task.FromResult(ret);
                },
                tools: ("gh_get", ReadOnlySchema, false));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(false, (bool?)obj["result"]?["isError"]);

            // Model-visible: an extra text block carrying the diagnostics JSON.
            var content = (JArray?)obj["result"]?["content"];
            Assert.NotNull(content);
            Assert.Equal(2, content!.Count);
            var diagnosticsJsonText = (string?)content[1]?["text"];
            Assert.NotNull(diagnosticsJsonText);
            var diagnosticsText = JObject.Parse(diagnosticsJsonText);
            var forwarded = (JArray?)diagnosticsText["diagnostics"];
            var diagnostic = Assert.Single(forwarded!);
            Assert.Equal("Warning", (string?)diagnostic["severity"]);
            Assert.Equal("Tool", (string?)diagnostic["origin"]);
            Assert.Contains("bogus", (string?)diagnostic["message"]);

            // Machine-readable: same projection under _meta, payload untouched.
            var meta = (JArray?)obj["result"]?["_meta"]?["smarthopper/diagnostics"];
            Assert.NotNull(meta);
            Assert.Equal("Warning", (string?)meta[0]?["severity"]);
            var firstTextJson = (string?)content[0]?["text"];
            Assert.NotNull(firstTextJson);
            var firstText = JObject.Parse(firstTextJson);
            Assert.True((bool?)firstText["ok"]);
            Assert.Null(firstText["diagnostics"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_ErrorWithPartialPayload_ReportsErrorAndKeepsResult()
        {
            var dispatcher = BuildDispatcher(new McpServerOptions { ExposeMutatingTools = true },
                executor: call =>
                {
                    var ret = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["placed"] = 2 },
                    }));
                    ret.AddRuntimeMessage(
                        SmartHopper.ProviderSdk.Diagnostics.SHRuntimeMessageSeverity.Error,
                        SmartHopper.ProviderSdk.Diagnostics.SHRuntimeMessageOrigin.Tool,
                        "apply failed midway");
                    return Task.FromResult(ret);
                },
                tools: ("gh_put", ReadOnlySchema, true));

            var raw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_put\",\"arguments\":{}}}");

            var obj = JObject.Parse(raw!);
            Assert.Equal(true, (bool?)obj["result"]?["isError"]);
            var textJson = (string?)obj["result"]?["content"]?[0]?["text"];
            Assert.NotNull(textJson);
            var text = JObject.Parse(textJson);
            Assert.Equal("apply failed midway", (string?)text["error"]);
            Assert.Equal(2, (int?)text["result"]?["placed"]);
            Assert.Equal(2, (int?)obj["result"]?["structuredContent"]?["result"]?["placed"]);
        }

        [Fact]
        public async Task Dispatch_ToolsCall_RunsConcurrentlyWithoutGlobalGate()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = 0;
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: async call =>
                {
                    Interlocked.Increment(ref entered);
                    await release.Task.ConfigureAwait(false);
                    var ret = new AIReturn { SkipRequestValidation = true, SkipMetricsValidation = true };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["ok"] = true },
                    }));
                    return ret;
                },
                tools: ("gh_get", ReadOnlySchema, false));

            var first = dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}");
            var second = dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}");

            // Both executions must enter the executor before either completes —
            // the removed all-method gate would have serialized the second call.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (Volatile.Read(ref entered) < 2 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal(2, Volatile.Read(ref entered));
            release.SetResult(true);
            var firstRaw = await first;
            var secondRaw = await second;
            Assert.Equal(false, (bool?)JObject.Parse(firstRaw!)["result"]?["isError"]);
            Assert.Equal(false, (bool?)JObject.Parse(secondRaw!)["result"]?["isError"]);
        }

        [Fact]
        public async Task Dispatch_SaturatedLane_ControlMethodsStayResponsive_AndExcessToolsRejected()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = 0;
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: async call =>
                {
                    Interlocked.Increment(ref entered);
                    await release.Task.ConfigureAwait(false);
                    var ret = new AIReturn { SkipRequestValidation = true, SkipMetricsValidation = true };
                    ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
                    {
                        Name = call.GetToolCall().Name,
                        Result = new JObject { ["ok"] = true },
                    }));
                    return ret;
                },
                tools: ("gh_get", ReadOnlySchema, false));

            // Occupy all 12 admitted slots with blocked tool calls.
            var calls = new List<Task<string?>>();
            for (var i = 0; i < 12; i++)
            {
                calls.Add(dispatcher.DispatchAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{100 + i},\"method\":\"tools/call\",\"params\":{{\"name\":\"gh_get\",\"arguments\":{{}}}}}}"));
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (Volatile.Read(ref entered) < 12 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal(12, Volatile.Read(ref entered));

            // Control plane stays responsive while the tool lane is saturated.
            var pingRaw = await dispatcher.DispatchAsync("{\"jsonrpc\":\"2.0\",\"id\":200,\"method\":\"ping\"}");
            Assert.NotNull(JObject.Parse(pingRaw!)["result"]);

            var listRaw = await dispatcher.DispatchAsync("{\"jsonrpc\":\"2.0\",\"id\":201,\"method\":\"tools/list\"}");
            Assert.NotNull((JArray?)JObject.Parse(listRaw!)["result"]?["tools"]);

            // Excess tool work is rejected promptly rather than queued unboundedly.
            var rejectedRaw = await dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":202,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}");
            var rejected = JObject.Parse(rejectedRaw!);
            Assert.Equal(-32603, (int?)rejected["error"]?["code"]);
            Assert.Contains("server_busy", (string?)rejected["error"]?["message"]);
            Assert.Equal(12, Volatile.Read(ref entered));

            release.SetResult(true);
            await Task.WhenAll(calls);
            foreach (var call in calls)
            {
                Assert.Equal(false, (bool?)JObject.Parse(call.Result!)["result"]?["isError"]);
            }
        }

        [Fact]
        public async Task Dispatch_SaturatedLane_CancelledQueuedCallDrops()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = 0;
            var dispatcher = BuildDispatcher(new McpServerOptions(),
                executor: async call =>
                {
                    Interlocked.Increment(ref entered);
                    await release.Task.ConfigureAwait(false);
                    return new AIReturn { SkipRequestValidation = true, SkipMetricsValidation = true };
                },
                tools: ("gh_get", ReadOnlySchema, false));

            var calls = new List<Task<string?>>();
            for (var i = 0; i < 12; i++)
            {
                calls.Add(dispatcher.DispatchAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{300 + i},\"method\":\"tools/call\",\"params\":{{\"name\":\"gh_get\",\"arguments\":{{}}}}}}"));
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (Volatile.Read(ref entered) < 12 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            using var cts = new CancellationTokenSource();
            var queued = dispatcher.DispatchAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":400,\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}",
                cts.Token);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

            release.SetResult(true);
            await Task.WhenAll(calls);
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
