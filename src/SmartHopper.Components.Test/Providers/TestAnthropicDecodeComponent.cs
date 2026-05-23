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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.Diagnostics;
using SmartHopper.Providers.Anthropic;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for Anthropic comprehensive response decoding.
    /// </summary>
    public class TestAnthropicDecodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("9B40A72D-F7AA-4C37-BFFF-A3C58DA6838F");

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public TestAnthropicDecodeComponent()
            : base("Test Anthropic Decode", "TEST-ANTHROPIC-DEC", "Tests Anthropic comprehensive response decoding", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Anthropic");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Text Decode Success", "TDS", "Text decoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Tool Call Decode Success", "TCDS", "Tool call decoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Tool Result Decode Success", "TRDS", "Tool result decoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Error Decode Success", "EDS", "Error decoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Metrics Success", "MS", "Metrics extraction test passed", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _textDecodeSuccess = new GH_Boolean(false);
            private GH_Boolean _toolCallDecodeSuccess = new GH_Boolean(false);
            private GH_Boolean _toolResultDecodeSuccess = new GH_Boolean(false);
            private GH_Boolean _errorDecodeSuccess = new GH_Boolean(false);
            private GH_Boolean _metricsSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestAnthropicDecodeComponent _parent;

            public Worker(TestAnthropicDecodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var provider = AnthropicProvider.Instance;

                    // Test 1: Basic text response decoding
                    this._messages.Add(new GH_String("=== Test 1: Text Response Decoding ==="));
                    bool textTest = await TestTextDecoding(provider);
                    this._textDecodeSuccess = new GH_Boolean(textTest);

                    // Test 2: Tool call decoding
                    this._messages.Add(new GH_String("=== Test 2: Tool Call Decoding ==="));
                    bool toolCallTest = await TestToolCallDecoding(provider);
                    this._toolCallDecodeSuccess = new GH_Boolean(toolCallTest);

                    // Test 3: Tool result decoding
                    this._messages.Add(new GH_String("=== Test 3: Tool Result Decoding ==="));
                    bool toolResultTest = await TestToolResultDecoding(provider);
                    this._toolResultDecodeSuccess = new GH_Boolean(toolResultTest);

                    // Test 4: Error response decoding
                    this._messages.Add(new GH_String("=== Test 4: Error Response Decoding ==="));
                    bool errorTest = await TestErrorDecoding(provider);
                    this._errorDecodeSuccess = new GH_Boolean(errorTest);

                    // Test 5: Metrics extraction
                    this._messages.Add(new GH_String("=== Test 5: Metrics Extraction ==="));
                    bool metricsTest = await TestMetricsExtraction(provider);
                    this._metricsSuccess = new GH_Boolean(metricsTest);

                    // Overall summary
                    this._messages.Add(new GH_String("=== Test Summary ==="));
                    this._messages.Add(new GH_String($"Text Decode: {(textTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Tool Call Decode: {(toolCallTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Tool Result Decode: {(toolResultTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Error Decode: {(errorTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Metrics: {(metricsTest ? "PASS" : "FAIL")}"));
                }
                catch (Exception ex)
                {
                    this._textDecodeSuccess = new GH_Boolean(false);
                    this._toolCallDecodeSuccess = new GH_Boolean(false);
                    this._toolResultDecodeSuccess = new GH_Boolean(false);
                    this._errorDecodeSuccess = new GH_Boolean(false);
                    this._metricsSuccess = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            private async Task<bool> TestTextDecoding(AnthropicProvider provider)
            {
                try
                {
                    // Test simple text response
                    var textResponse = new JObject
                    {
                        ["id"] = "msg_abc123",
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = "This is an Anthropic test response"
                            }
                        },
                        ["stop_reason"] = "end_turn",
                        ["model"] = "claude-3-5-sonnet-20241022",
                        ["usage"] = new JObject
                        {
                            ["input_tokens"] = 14,
                            ["output_tokens"] = 9
                        }
                    };

                    var interactions = provider.Decode(textResponse);
                    var textInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault();
                    if (textInteraction == null || string.IsNullOrEmpty(textInteraction.Content))
                    {
                        this._messages.Add(new GH_String("✗ Text interaction not decoded"));
                        return false;
                    }

                    if (!textInteraction.Content.Contains("Anthropic test response"))
                    {
                        this._messages.Add(new GH_String("✗ Content doesn't match expected response"));
                        return false;
                    }

                    // Test thinking extraction (thinking field in content array)
                    var thinkingResponse = new JObject
                    {
                        ["id"] = "msg_def456",
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "thinking",
                                ["thinking"] = "Let me analyze this step by step..."
                            },
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = "Here is my final answer."
                            }
                        },
                        ["stop_reason"] = "end_turn",
                        ["model"] = "claude-3-5-sonnet-20241022"
                    };

                    var thinkingInteractions = provider.Decode(thinkingResponse);
                    var thinkingText = thinkingInteractions.OfType<AIInteractionText>().FirstOrDefault(i => i.Reasoning != null);
                    if (thinkingText == null)
                    {
                        this._messages.Add(new GH_String("✗ Thinking not decoded from thinking field"));
                        return false;
                    }

                    if (!thinkingText.Reasoning.Contains("Let me analyze"))
                    {
                        this._messages.Add(new GH_String("✗ Thinking content incorrect"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Text decoding successful"));
                    this._messages.Add(new GH_String("✓ Simple text response decoded"));
                    this._messages.Add(new GH_String("✓ Thinking extracted from thinking field"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Text decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestToolCallDecoding(AnthropicProvider provider)
            {
                try
                {
                    // Test tool call decoding (tool_use block in content array)
                    var toolResponse = new JObject
                    {
                        ["id"] = "msg_ghi789",
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = "toolu_abc123",
                                ["name"] = "search_web",
                                ["input"] = new JObject
                                {
                                    ["query"] = "test search"
                                }
                            }
                        },
                        ["stop_reason"] = "tool_use",
                        ["model"] = "claude-3-5-sonnet-20241022"
                    };

                    var interactions = provider.Decode(toolResponse);
                    var toolCall = interactions.OfType<AIInteractionToolCall>().FirstOrDefault();
                    if (toolCall == null)
                    {
                        this._messages.Add(new GH_String("✗ Tool call not decoded"));
                        return false;
                    }

                    if (toolCall.Name != "search_web")
                    {
                        this._messages.Add(new GH_String($"✗ Tool name incorrect: expected 'search_web', got '{toolCall.Name}'"));
                        return false;
                    }

                    if (toolCall.Id != "toolu_abc123")
                    {
                        this._messages.Add(new GH_String($"✗ Tool call ID incorrect: expected 'toolu_abc123', got '{toolCall.Id}'"));
                        return false;
                    }

                    if (toolCall.Arguments == null || toolCall.Arguments["query"]?.ToString() != "test search")
                    {
                        this._messages.Add(new GH_String("✗ Tool arguments not decoded correctly"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Tool call decoding successful"));
                    this._messages.Add(new GH_String("✓ Tool call decoded with correct name and arguments"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Tool call decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestToolResultDecoding(AnthropicProvider provider)
            {
                try
                {
                    // Test tool result decoding (tool_result block in content array)
                    var toolResultResponse = new JObject
                    {
                        ["id"] = "msg_def456",
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = "toolu_abc123",
                                ["content"] = "Search results: 3 items found"
                            }
                        },
                        ["model"] = "claude-3-5-sonnet-20241022"
                    };

                    var interactions = provider.Decode(toolResultResponse);
                    var toolResult = interactions.OfType<AIInteractionToolResult>().FirstOrDefault();
                    if (toolResult == null)
                    {
                        this._messages.Add(new GH_String("✗ Tool result not decoded"));
                        return false;
                    }

                    if (toolResult.Id != "toolu_abc123")
                    {
                        this._messages.Add(new GH_String($"✗ Tool result ID incorrect: expected 'toolu_abc123', got '{toolResult.Id}'"));
                        return false;
                    }

                    if (toolResult.Result == null || toolResult.Result["content"]?.ToString() != "Search results: 3 items found")
                    {
                        this._messages.Add(new GH_String("✗ Tool result content not decoded correctly"));
                        return false;
                    }

                    // Test tool result with JSON content
                    var jsonToolResultResponse = new JObject
                    {
                        ["id"] = "msg_ghi789",
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = "toolu_def456",
                                ["content"] = new JObject
                                {
                                    ["results"] = 5,
                                    ["status"] = "success"
                                }
                            }
                        },
                        ["model"] = "claude-3-5-sonnet-20241022"
                    };

                    var jsonInteractions = provider.Decode(jsonToolResultResponse);
                    var jsonToolResult = jsonInteractions.OfType<AIInteractionToolResult>().FirstOrDefault();
                    if (jsonToolResult == null)
                    {
                        this._messages.Add(new GH_String("✗ JSON tool result not decoded"));
                        return false;
                    }

                    if (jsonToolResult.Result == null)
                    {
                        this._messages.Add(new GH_String("✗ JSON tool result content is null"));
                        return false;
                    }

                    // Parse the content to verify it's valid JSON
                    var jsonContent = jsonToolResult.Result.ToString();
                    var parsedJson = JObject.Parse(jsonContent);
                    if (parsedJson["results"]?.ToObject<int>() != 5)
                    {
                        this._messages.Add(new GH_String("✗ JSON tool result results incorrect"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Tool result decoding successful"));
                    this._messages.Add(new GH_String("✓ Tool result decoded with correct ID and content"));
                    this._messages.Add(new GH_String("✓ JSON tool result decoded correctly"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Tool result decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestErrorDecoding(AnthropicProvider provider)
            {
                try
                {
                    // Test error format 1: Authentication error
                    var errorResponse1 = new JObject
                    {
                        ["type"] = "error",
                        ["error"] = new JObject
                        {
                            ["type"] = "authentication_error",
                            ["message"] = "Invalid API key"
                        }
                    };

                    var interactions1 = provider.Decode(errorResponse1);
                    var runtimeMessage1 = interactions1.OfType<AIInteractionRuntimeMessage>().FirstOrDefault();
                    if (runtimeMessage1 == null || runtimeMessage1.Severity != SHRuntimeMessageSeverity.Error)
                    {
                        this._messages.Add(new GH_String("✗ Error format 1 not handled correctly"));
                        return false;
                    }
                    if (!runtimeMessage1.Content.Contains("Invalid API key"))
                    {
                        this._messages.Add(new GH_String("✗ Error message not extracted correctly (format 1)"));
                        return false;
                    }

                    // Test error format 2: Rate limit error
                    var errorResponse2 = new JObject
                    {
                        ["type"] = "error",
                        ["error"] = new JObject
                        {
                            ["type"] = "rate_limit_error",
                            ["message"] = "Rate limit exceeded"
                        }
                    };

                    var interactions2 = provider.Decode(errorResponse2);
                    var runtimeMessage2 = interactions2.OfType<AIInteractionRuntimeMessage>().FirstOrDefault();
                    if (runtimeMessage2 == null || runtimeMessage2.Severity != SHRuntimeMessageSeverity.Error)
                    {
                        this._messages.Add(new GH_String("✗ Error format 2 not handled correctly"));
                        return false;
                    }
                    if (!runtimeMessage2.Content.Contains("Rate limit exceeded"))
                    {
                        this._messages.Add(new GH_String("✗ Error message not extracted correctly (format 2)"));
                        return false;
                    }

                    // Test error format 3: Invalid request error
                    var errorResponse3 = new JObject
                    {
                        ["type"] = "error",
                        ["error"] = new JObject
                        {
                            ["type"] = "invalid_request_error",
                            ["message"] = "Invalid request: model not found"
                        }
                    };

                    var interactions3 = provider.Decode(errorResponse3);
                    var runtimeMessage3 = interactions3.OfType<AIInteractionRuntimeMessage>().FirstOrDefault();
                    if (runtimeMessage3 == null || runtimeMessage3.Severity != SHRuntimeMessageSeverity.Error)
                    {
                        this._messages.Add(new GH_String("✗ Error format 3 not handled correctly"));
                        return false;
                    }
                    if (!runtimeMessage3.Content.Contains("Invalid request"))
                    {
                        this._messages.Add(new GH_String("✗ Error message not extracted correctly (format 3)"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Error decoding successful"));
                    this._messages.Add(new GH_String("✓ Authentication error handled"));
                    this._messages.Add(new GH_String("✓ Rate limit error handled"));
                    this._messages.Add(new GH_String("✓ Invalid request error handled"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Error decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestMetricsExtraction(AnthropicProvider provider)
            {
                try
                {
                    // Anthropic uses input_tokens, output_tokens, cache_read_input_tokens
                    var responseWithMetrics = new JObject
                    {
                        ["id"] = "msg_jkl012",
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = "Test response"
                            }
                        },
                        ["stop_reason"] = "end_turn",
                        ["model"] = "claude-3-5-sonnet-20241022",
                        ["usage"] = new JObject
                        {
                            ["input_tokens"] = 45,
                            ["output_tokens"] = 20,
                            ["cache_read_input_tokens"] = 15
                        }
                    };

                    var interactions = provider.Decode(responseWithMetrics);
                    var textInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault();
                    if (textInteraction == null)
                    {
                        this._messages.Add(new GH_String("✗ No text interaction decoded"));
                        return false;
                    }

                    var metrics = textInteraction.Metrics;
                    if (metrics == null)
                    {
                        this._messages.Add(new GH_String("✗ Metrics not extracted"));
                        return false;
                    }

                    // Anthropic calculates InputTokensPrompt = input_tokens - cache_read_input_tokens
                    if (metrics.InputTokensPrompt != 30) // 45 - 15
                    {
                        this._messages.Add(new GH_String($"✗ Input tokens incorrect: expected 30, got {metrics.InputTokensPrompt}"));
                        return false;
                    }

                    if (metrics.InputTokensCached != 15)
                    {
                        this._messages.Add(new GH_String($"✗ Cached tokens incorrect: expected 15, got {metrics.InputTokensCached}"));
                        return false;
                    }

                    if (metrics.OutputTokensGeneration != 20)
                    {
                        this._messages.Add(new GH_String($"✗ Output tokens incorrect: expected 20, got {metrics.OutputTokensGeneration}"));
                        return false;
                    }

                    if (metrics.FinishReason != "end_turn")
                    {
                        this._messages.Add(new GH_String($"✗ Finish reason incorrect: expected 'end_turn', got {metrics.FinishReason}"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Metrics extraction successful"));
                    this._messages.Add(new GH_String($"✓ Input tokens: {metrics.InputTokensPrompt} (cached: {metrics.InputTokensCached})"));
                    this._messages.Add(new GH_String($"✓ Output tokens: {metrics.OutputTokensGeneration}"));
                    this._messages.Add(new GH_String($"✓ Finish reason: {metrics.FinishReason}"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Metrics extraction error: {ex.Message}"));
                    return false;
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Text Decode Success", this._textDecodeSuccess, DA);
                this._parent.SetPersistentOutput("Tool Call Decode Success", this._toolCallDecodeSuccess, DA);
                this._parent.SetPersistentOutput("Tool Result Decode Success", this._toolResultDecodeSuccess, DA);
                this._parent.SetPersistentOutput("Error Decode Success", this._errorDecodeSuccess, DA);
                this._parent.SetPersistentOutput("Metrics Success", this._metricsSuccess, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);

                bool allPassed = this._textDecodeSuccess.Value && this._toolCallDecodeSuccess.Value && 
                               this._toolResultDecodeSuccess.Value && this._errorDecodeSuccess.Value && this._metricsSuccess.Value;
                message = allPassed ? "Anthropic decoding tests passed" : "Anthropic decoding tests failed";
            }
        }
    }
}
