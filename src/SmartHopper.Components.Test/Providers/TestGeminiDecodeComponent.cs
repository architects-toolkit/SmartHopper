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
using SmartHopper.Providers.Gemini;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for Gemini comprehensive response decoding.
    /// </summary>
    public class TestGeminiDecodeComponent : AIStatefulAsyncComponentBase
    {

        public override Guid ComponentGuid => new Guid("BA230457-1318-4346-A6F3-453F32EDBD38");

        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public TestGeminiDecodeComponent()
            : base("Test Gemini Decode", "TEST-GEMINI-DEC", "Tests Gemini comprehensive response decoding", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Gemini");
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
            private readonly TestGeminiDecodeComponent _parent;

            public Worker(TestGeminiDecodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var provider = GeminiProvider.Instance;

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

            private async Task<bool> TestTextDecoding(GeminiProvider provider)
            {
                try
                {
                    // Test simple text response
                    var textResponse = new JObject
                    {
                        ["candidates"] = new JArray
                        {
                            new JObject
                            {
                                ["content"] = new JObject
                                {
                                    ["parts"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["text"] = "This is a Gemini test response"
                                        }
                                    }
                                },
                                ["finishReason"] = "STOP"
                            }
                        },
                        ["usageMetadata"] = new JObject
                        {
                            ["promptTokenCount"] = 25,
                            ["candidatesTokenCount"] = 12,
                            ["totalTokenCount"] = 37
                        },
                        ["model"] = "gemini-1.5-flash",
                        ["id"] = "gemini-123"
                    };

                    var interactions = provider.Decode(textResponse);
                    var textInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault();
                    if (textInteraction == null || string.IsNullOrEmpty(textInteraction.Content))
                    {
                        this._messages.Add(new GH_String("✗ Text interaction not decoded"));
                        return false;
                    }

                    if (!textInteraction.Content.Contains("Gemini test response"))
                    {
                        this._messages.Add(new GH_String("✗ Content doesn't match expected response"));
                        return false;
                    }

                    // Test reasoning extraction (thought field in parts)
                    var reasoningResponse = new JObject
                    {
                        ["candidates"] = new JArray
                        {
                            new JObject
                            {
                                ["content"] = new JObject
                                {
                                    ["parts"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["thought"] = true,
                                            ["text"] = "Let me think about this..."
                                        },
                                        new JObject
                                        {
                                            ["text"] = "Here is the answer."
                                        }
                                    }
                                },
                                ["finishReason"] = "STOP"
                            }
                        },
                        ["model"] = "gemini-1.5-flash",
                        ["id"] = "gemini-456"
                    };

                    var reasoningInteractions = provider.Decode(reasoningResponse);
                    var reasoningText = reasoningInteractions.OfType<AIInteractionText>().FirstOrDefault(i => i.Reasoning != null);
                    if (reasoningText == null)
                    {
                        this._messages.Add(new GH_String("✗ Reasoning not decoded from thought field"));
                        return false;
                    }

                    if (!reasoningText.Reasoning.Contains("Let me think"))
                    {
                        this._messages.Add(new GH_String("✗ Reasoning content incorrect"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Text decoding successful"));
                    this._messages.Add(new GH_String("✓ Simple text response decoded"));
                    this._messages.Add(new GH_String("✓ Reasoning extracted from thought field"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Text decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestToolCallDecoding(GeminiProvider provider)
            {
                try
                {
                    // Test tool call decoding (functionCall in parts array)
                    var toolResponse = new JObject
                    {
                        ["candidates"] = new JArray
                        {
                            new JObject
                            {
                                ["content"] = new JObject
                                {
                                    ["parts"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["functionCall"] = new JObject
                                            {
                                                ["id"] = "call_gem789",
                                                ["name"] = "search_database",
                                                ["args"] = new JObject
                                                {
                                                    ["query"] = "test query"
                                                }
                                            }
                                        }
                                    }
                                },
                                ["finishReason"] = "STOP"
                            }
                        },
                        ["model"] = "gemini-1.5-flash",
                        ["id"] = "gemini-789"
                    };

                    var interactions = provider.Decode(toolResponse);
                    var toolCall = interactions.OfType<AIInteractionToolCall>().FirstOrDefault();
                    if (toolCall == null)
                    {
                        this._messages.Add(new GH_String("✗ Tool call not decoded"));
                        return false;
                    }

                    if (toolCall.Name != "search_database")
                    {
                        this._messages.Add(new GH_String($"✗ Tool name incorrect: expected 'search_database', got '{toolCall.Name}'"));
                        return false;
                    }

                    if (toolCall.Id != "call_gem789")
                    {
                        this._messages.Add(new GH_String($"✗ Tool call ID incorrect: expected 'call_gem789', got '{toolCall.Id}'"));
                        return false;
                    }

                    if (toolCall.Arguments == null || toolCall.Arguments["query"]?.ToString() != "test query")
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

            private async Task<bool> TestErrorDecoding(GeminiProvider provider)
            {
                try
                {
                    // Test error format 1: Invalid argument (400)
                    var errorResponse1 = new JObject
                    {
                        ["error"] = new JObject
                        {
                            ["code"] = 400,
                            ["message"] = "Invalid request: missing required field",
                            ["status"] = "INVALID_ARGUMENT"
                        }
                    };

                    var interactions1 = provider.Decode(errorResponse1);
                    var runtimeMessage1 = interactions1.OfType<AIInteractionRuntimeMessage>().FirstOrDefault();
                    if (runtimeMessage1 == null || runtimeMessage1.Severity != SHRuntimeMessageSeverity.Error)
                    {
                        this._messages.Add(new GH_String("✗ Error format 1 not handled correctly"));
                        return false;
                    }
                    if (!runtimeMessage1.Content.Contains("Invalid request"))
                    {
                        this._messages.Add(new GH_String("✗ Error message not extracted correctly (format 1)"));
                        return false;
                    }

                    // Test error format 2: Authentication error (401)
                    var errorResponse2 = new JObject
                    {
                        ["error"] = new JObject
                        {
                            ["code"] = 401,
                            ["message"] = "Request had invalid credentials",
                            ["status"] = "UNAUTHENTICATED"
                        }
                    };

                    var interactions2 = provider.Decode(errorResponse2);
                    var runtimeMessage2 = interactions2.OfType<AIInteractionRuntimeMessage>().FirstOrDefault();
                    if (runtimeMessage2 == null || runtimeMessage2.Severity != SHRuntimeMessageSeverity.Error)
                    {
                        this._messages.Add(new GH_String("✗ Error format 2 not handled correctly"));
                        return false;
                    }
                    if (!runtimeMessage2.Content.Contains("invalid credentials"))
                    {
                        this._messages.Add(new GH_String("✗ Error message not extracted correctly (format 2)"));
                        return false;
                    }

                    // Test error format 3: Rate limit error (429)
                    var errorResponse3 = new JObject
                    {
                        ["error"] = new JObject
                        {
                            ["code"] = 429,
                            ["message"] = "Resource has been exhausted",
                            ["status"] = "RESOURCE_EXHAUSTED"
                        }
                    };

                    var interactions3 = provider.Decode(errorResponse3);
                    var runtimeMessage3 = interactions3.OfType<AIInteractionRuntimeMessage>().FirstOrDefault();
                    if (runtimeMessage3 == null || runtimeMessage3.Severity != SHRuntimeMessageSeverity.Error)
                    {
                        this._messages.Add(new GH_String("✗ Error format 3 not handled correctly"));
                        return false;
                    }
                    if (!runtimeMessage3.Content.Contains("exhausted"))
                    {
                        this._messages.Add(new GH_String("✗ Error message not extracted correctly (format 3)"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Error decoding successful"));
                    this._messages.Add(new GH_String("✓ Invalid argument error handled"));
                    this._messages.Add(new GH_String("✓ Authentication error handled"));
                    this._messages.Add(new GH_String("✓ Rate limit error handled"));
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"✗ Error decoding error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestMetricsExtraction(GeminiProvider provider)
            {
                try
                {
                    // Gemini uses usageMetadata with different field names
                    var responseWithMetrics = new JObject
                    {
                        ["candidates"] = new JArray
                        {
                            new JObject
                            {
                                ["content"] = new JObject
                                {
                                    ["parts"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["text"] = "Test response"
                                        }
                                    }
                                },
                                ["finishReason"] = "STOP"
                            }
                        },
                        ["usageMetadata"] = new JObject
                        {
                            ["promptTokenCount"] = 35,
                            ["candidatesTokenCount"] = 18,
                            ["totalTokenCount"] = 53,
                            ["thoughtsTokenCount"] = 7
                        },
                        ["model"] = "gemini-1.5-flash",
                        ["id"] = "gemini-123"
                    };

                    var interactions = provider.Decode(responseWithMetrics);
                    
                    // Gemini creates a separate metrics interaction
                    var metricsInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault(i => i.Metrics != null);
                    if (metricsInteraction == null)
                    {
                        this._messages.Add(new GH_String("✗ No metrics interaction decoded"));
                        return false;
                    }

                    var metrics = metricsInteraction.Metrics;
                    if (metrics == null)
                    {
                        this._messages.Add(new GH_String("✗ Metrics not extracted"));
                        return false;
                    }

                    if (metrics.InputTokensPrompt != 35)
                    {
                        this._messages.Add(new GH_String($"✗ Input tokens incorrect: expected 35, got {metrics.InputTokensPrompt}"));
                        return false;
                    }

                    if (metrics.OutputTokensGeneration != 18)
                    {
                        this._messages.Add(new GH_String($"✗ Output tokens incorrect: expected 18, got {metrics.OutputTokensGeneration}"));
                        return false;
                    }

                    if (metrics.OutputTokensReasoning != 7)
                    {
                        this._messages.Add(new GH_String($"✗ Reasoning tokens incorrect: expected 7, got {metrics.OutputTokensReasoning}"));
                        return false;
                    }

                    if (metrics.FinishReason != "STOP")
                    {
                        this._messages.Add(new GH_String($"✗ Finish reason incorrect: expected 'STOP', got {metrics.FinishReason}"));
                        return false;
                    }

                    this._messages.Add(new GH_String("✓ Metrics extraction successful"));
                    this._messages.Add(new GH_String($"✓ Input tokens: {metrics.InputTokensPrompt}"));
                    this._messages.Add(new GH_String($"✓ Output tokens: {metrics.OutputTokensGeneration} (reasoning: {metrics.OutputTokensReasoning})"));
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
                message = allPassed ? "Gemini decoding tests passed" : "Gemini decoding tests failed";
            }
        }
    }
}
