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
using SmartHopper.Providers.MistralAI;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for MistralAI comprehensive response decoding.
    /// </summary>
    public class TestMistralAIDecodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("8D2545BA-C1BC-45DD-B94B-A07838C00B15");

        public TestMistralAIDecodeComponent()
            : base("Test MistralAI Decode", "TEST-MISTRAL-DEC", "Tests MistralAI comprehensive response decoding", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("MistralAI");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Text Decode Success", "TDS", "Text decoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Tool Call Decode Success", "TCDS", "Tool call decoding test passed", GH_ParamAccess.item);
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
            private GH_Boolean _errorDecodeSuccess = new GH_Boolean(false);
            private GH_Boolean _metricsSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestMistralAIDecodeComponent _parent;

            public Worker(TestMistralAIDecodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var provider = MistralAIProvider.Instance;

                    // Test 1: Basic text response decoding
                    this._messages.Add(new GH_String("=== Test 1: Text Response Decoding ==="));
                    bool textTest = await TestTextDecoding(provider);
                    this._textDecodeSuccess = new GH_Boolean(textTest);

                    // Test 2: Tool call decoding
                    this._messages.Add(new GH_String("=== Test 2: Tool Call Decoding ==="));
                    bool toolCallTest = await TestToolCallDecoding(provider);
                    this._toolCallDecodeSuccess = new GH_Boolean(toolCallTest);

                    // Test 3: Error response decoding
                    this._messages.Add(new GH_String("=== Test 3: Error Response Decoding ==="));
                    bool errorTest = await TestErrorDecoding(provider);
                    this._errorDecodeSuccess = new GH_Boolean(errorTest);

                    // Test 4: Metrics extraction
                    this._messages.Add(new GH_String("=== Test 4: Metrics Extraction ==="));
                    bool metricsTest = await TestMetricsExtraction(provider);
                    this._metricsSuccess = new GH_Boolean(metricsTest);

                    // Overall summary
                    this._messages.Add(new GH_String("=== Test Summary ==="));
                    this._messages.Add(new GH_String($"Text Decode: {(textTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Tool Call Decode: {(toolCallTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Error Decode: {(errorTest ? "PASS" : "FAIL")}"));
                    this._messages.Add(new GH_String($"Metrics: {(metricsTest ? "PASS" : "FAIL")}"));
                }
                catch (Exception ex)
                {
                    this._textDecodeSuccess = new GH_Boolean(false);
                    this._toolCallDecodeSuccess = new GH_Boolean(false);
                    this._errorDecodeSuccess = new GH_Boolean(false);
                    this._metricsSuccess = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            private async Task<bool> TestTextDecoding(MistralAIProvider provider)
            {
                try
                {
                    // Test simple text response
                    var textResponse = new JObject
                    {
                        ["choices"] = new JArray
                        {
                            new JObject
                            {
                                ["message"] = new JObject
                                {
                                    ["role"] = "assistant",
                                    ["content"] = "This is a MistralAI test response"
                                },
                                ["finish_reason"] = "stop"
                            }
                        },
                        ["usage"] = new JObject
                        {
                            ["prompt_tokens"] = 12,
                            ["completion_tokens"] = 6,
                            ["total_tokens"] = 18
                        },
                        ["model"] = "mistral-small-latest",
                        ["id"] = "mistral-123"
                    };

                    var interactions = provider.Decode(textResponse);
                    var textInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault();
                    if (textInteraction == null || string.IsNullOrEmpty(textInteraction.Content))
                    {
                        this._messages.Add(new GH_String("✗ Text interaction not decoded"));
                        return false;
                    }

                    if (!textInteraction.Content.Contains("This is a MistralAI test response"))
                    {
                        this._messages.Add(new GH_String("✗ Content doesn't match expected response"));
                        return false;
                    }

                    // Test structured content array with thinking
                    var structuredResponse = new JObject
                    {
                        ["choices"] = new JArray
                        {
                            new JObject
                            {
                                ["message"] = new JObject
                                {
                                    ["role"] = "assistant",
                                    ["content"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["type"] = "thinking",
                                            ["thinking"] = new JArray
                                            {
                                                new JObject
                                                {
                                                    ["type"] = "text",
                                                    ["text"] = "Let me think about this...",
                                                },
                                            },
                                        },
                                        new JObject
                                        {
                                           ["type"] = "text",
                                           ["text"] = "Here is the answer.",
                                        },
                                    },
                                },
                                ["finish_reason"] = "stop",
                            },
                        },
                        ["model"] = "mistral-small-latest",
                        ["id"] = "mistral-456",
                    };

                    var structuredInteractions = provider.Decode(structuredResponse);
                    var structuredText = structuredInteractions.OfType<AIInteractionText>().FirstOrDefault();
                    if (structuredText == null)
                    {
                        this._messages.Add(new GH_String("✗ Structured content not decoded"));
                        return false;
                    }

                    if (structuredText.Reasoning == null || !structuredText.Reasoning.Contains("Let me think"))
                    {
                        this._messages.Add(new GH_String("✗ Reasoning not extracted from structured content"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Text decoding successful"));
                    this._messages.Add(new GH_String("✓ Simple text response decoded"));
                    this._messages.Add(new GH_String("✓ Structured content with thinking decoded"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Text decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestToolCallDecoding(MistralAIProvider provider)
            {
                try
                {
                    // Test tool call decoding
                    var toolResponse = new JObject
                    {
                        ["choices"] = new JArray
                        {
                            new JObject
                            {
                                ["message"] = new JObject
                                {
                                    ["role"] = "assistant",
                                    ["content"] = "I'll call a tool.",
                                    ["tool_calls"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["id"] = "call_abc123",
                                            ["type"] = "function",
                                            ["function"] = new JObject
                                            {
                                                ["name"] = "get_weather",
                                                ["arguments"] = "{\"location\": \"Paris\"}"
                                            }
                                        }
                                    }
                                },
                                ["finish_reason"] = "tool_calls"
                            }
                        },
                        ["model"] = "mistral-small-latest",
                        ["id"] = "mistral-789"
                    };

                    var interactions = provider.Decode(toolResponse);
                    var toolCall = interactions.OfType<AIInteractionToolCall>().FirstOrDefault();
                    if (toolCall == null)
                    {
                        this._messages.Add(new GH_String("✗ Tool call not decoded"));
                        return false;
                    }

                    if (toolCall.Name != "get_weather")
                    {
                        this._messages.Add(new GH_String($"✗ Tool name incorrect: expected 'get_weather', got '{toolCall.Name}'"));
                        return false;
                    }

                    if (toolCall.Id != "call_abc123")
                    {
                        this._messages.Add(new GH_String($"✗ Tool call ID incorrect: expected 'call_abc123', got '{toolCall.Id}'"));
                        return false;
                    }

                    if (toolCall.Arguments == null || toolCall.Arguments["location"]?.ToString() != "Paris")
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

            private async Task<bool> TestErrorDecoding(MistralAIProvider provider)
            {
                try
                {
                    // Test error format 1: {"error": {"message": "...", "type": "..."}}
                    var errorResponse1 = new JObject
                    {
                        ["error"] = new JObject
                        {
                            ["message"] = "Invalid API key",
                            ["type"] = "authentication_error"
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

                    // Test error format 2: {"message": "...", "code": "..."}
                    var errorResponse2 = new JObject
                    {
                        ["message"] = "Rate limit exceeded",
                        ["code"] = "rate_limit_error"
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

                    this._messages.Add(new GH_String("✓ Error decoding successful"));
                    this._messages.Add(new GH_String("✓ Error format 1 (error object) handled"));
                    this._messages.Add(new GH_String("✓ Error format 2 (message + code) handled"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Error decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestMetricsExtraction(MistralAIProvider provider)
            {
                try
                {
                    var responseWithMetrics = new JObject
                    {
                        ["choices"] = new JArray
                        {
                            new JObject
                            {
                                ["message"] = new JObject
                                {
                                    ["role"] = "assistant",
                                    ["content"] = "Test response"
                                },
                                ["finish_reason"] = "stop"
                            }
                        },
                        ["usage"] = new JObject
                        {
                            ["prompt_tokens"] = 42,
                            ["completion_tokens"] = 18,
                            ["total_tokens"] = 60
                        },
                        ["model"] = "mistral-small-latest",
                        ["id"] = "test-123"
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

                    if (metrics.InputTokensPrompt != 42)
                    {
                        this._messages.Add(new GH_String($"✗ Input tokens incorrect: expected 42, got {metrics.InputTokensPrompt}"));
                        return false;
                    }

                    if (metrics.OutputTokensGeneration != 18)
                    {
                        this._messages.Add(new GH_String($"✗ Output tokens incorrect: expected 18, got {metrics.OutputTokensGeneration}"));
                        return false;
                    }

                    if (metrics.FinishReason != "stop")
                    {
                        this._messages.Add(new GH_String($"✗ Finish reason incorrect: expected 'stop', got {metrics.FinishReason}"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Metrics extraction successful"));
                    this._messages.Add(new GH_String($"✓ Input tokens: {metrics.InputTokensPrompt}"));
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
                this._parent.SetPersistentOutput("Error Decode Success", this._errorDecodeSuccess, DA);
                this._parent.SetPersistentOutput("Metrics Success", this._metricsSuccess, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);

                bool allPassed = this._textDecodeSuccess.Value && this._toolCallDecodeSuccess.Value &&
                               this._errorDecodeSuccess.Value && this._metricsSuccess.Value;
                message = allPassed ? "MistralAI decoding tests passed" : "MistralAI decoding tests failed";
            }
        }
    }
}
