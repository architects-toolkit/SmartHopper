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
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides the "gh_smart_connect" AI tool for AI-powered component wiring.
    /// Given a set of component GUIDs and a purpose, retrieves their GhJSON,
    /// asks an AI model to suggest connections, and executes them via gh_connect.
    /// </summary>
    public partial class gh_smart_connect : IAIToolProvider
    {
        /// <summary>
        /// Regex to extract a JSON object from a markdown-fenced or bare AI response.
        /// </summary>
        [GeneratedRegex(@"\{[\s\S]*\}", RegexOptions.Singleline)]
        private static partial Regex JsonObjectRegex();

        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_smart_connect";

        /// <summary>
        /// System prompt instructing the AI to suggest Grasshopper connections.
        /// </summary>
        private readonly string systemPrompt =
            "You are a Grasshopper component wiring expert. " +
            "You are given a GhJSON document describing components on a canvas and a user-specified purpose. " +
            "Your task is to suggest which output parameters should be connected to which input parameters to achieve the stated purpose.\n\n" +
            "Rules:\n" +
            "- Only connect outputs to inputs (never input-to-input or output-to-output).\n" +
            "- Use the exact instanceGuid values from the GhJSON.\n" +
            "- Use exact parameter names from the GhJSON.\n" +
            "- Do not create duplicate connections.\n" +
            "- Only suggest connections between the provided components.\n" +
            "- If a connection is not possible or not needed, do not include it.\n\n" +
            "Return ONLY a valid JSON object in this exact format, with no additional text:\n" +
            "{\n" +
            "  \"connections\": [\n" +
            "    {\n" +
            "      \"sourceGuid\": \"<instanceGuid of source component>\",\n" +
            "      \"sourceParam\": \"<name of the output parameter>\",\n" +
            "      \"targetGuid\": \"<instanceGuid of target component>\",\n" +
            "      \"targetParam\": \"<name of the input parameter>\"\n" +
            "    }\n" +
            "  ],\n" +
            "  \"reasoning\": \"<brief explanation of why these connections achieve the purpose>\"\n" +
            "}";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "AI-powered smart connection tool. Given a set of component GUIDs and a purpose description, retrieves their structure via gh_get, asks an AI model to suggest optimal connections, and executes them via gh_connect. Returns the connection results and the AI reasoning.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Array of instanceGuid strings identifying the components to connect.""
                        },
                        ""purpose"": {
                            ""type"": ""string"",
                            ""description"": ""A text description of the desired wiring purpose or goal.""
                        }
                    },
                    ""required"": [""guids"", ""purpose""]
                }",
                execute: this.GhSmartConnectToolAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput);
        }

        /// <summary>
        /// Executes the gh_smart_connect tool.
        /// </summary>
        private async Task<AIReturn> GhSmartConnectToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                var guidsArray = args["guids"] as JArray;
                var purpose = args["purpose"]?.ToString() ?? string.Empty;

                if (guidsArray == null || guidsArray.Count == 0)
                {
                    output.CreateError("No GUIDs provided. The 'guids' parameter must be a non-empty array of instanceGuid strings.");
                    return output;
                }

                if (string.IsNullOrWhiteSpace(purpose))
                {
                    output.CreateError("No purpose provided. The 'purpose' parameter must describe the desired wiring goal.");
                    return output;
                }

                var guids = guidsArray.Select(g => g.ToString()).ToList();
                Debug.WriteLine($"[gh_smart_connect] Starting with {guids.Count} GUIDs, purpose: {purpose}");

                // Step 1: Retrieve GhJSON via gh_get
                var ghjson = await this.RetrieveGhJsonAsync(guids).ConfigureAwait(false);
                if (ghjson == null)
                {
                    output.CreateError("Failed to retrieve component data via gh_get. Ensure the provided GUIDs are valid and components exist on the canvas.");
                    return output;
                }

                Debug.WriteLine($"[gh_smart_connect] Retrieved GhJSON ({ghjson.Length} chars)");

                // Step 2: Ask AI for connection suggestions
                var (connectionsJson, reasoning, aiMetrics, aiMessages) = await this.GetAiSuggestionsAsync(
                    toolCall.Provider,
                    toolCall.Model,
                    ghjson,
                    purpose).ConfigureAwait(false);

                if (connectionsJson == null)
                {
                    output.CreateError("AI failed to suggest connections. The model did not return a valid response.");
                    return output;
                }

                Debug.WriteLine($"[gh_smart_connect] AI suggested {connectionsJson.Count} connection(s)");

                // Step 3: Execute connections via gh_connect
                var connectResult = await this.ExecuteConnectionsAsync(connectionsJson).ConfigureAwait(false);

                // Build combined result
                var toolResult = new JObject
                {
                    ["reasoning"] = reasoning ?? string.Empty,
                    ["suggestedConnections"] = connectionsJson,
                };

                if (connectResult != null)
                {
                    toolResult["connectionResult"] = connectResult;
                }

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(
                    toolResult,
                    toolInfo.Id,
                    toolInfo.Name,
                    aiMetrics,
                    aiMessages);

                var outImmutable = outBuilder.Build();
                output.CreateSuccess(outImmutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_smart_connect] Error: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Calls the gh_get tool to retrieve GhJSON for the specified GUIDs.
        /// </summary>
        /// <param name="guids">List of instanceGuid strings.</param>
        /// <returns>The GhJSON string, or null on failure.</returns>
        private async Task<string> RetrieveGhJsonAsync(List<string> guids)
        {
            try
            {
                var ghGetParams = new JObject
                {
                    ["guidFilter"] = JArray.FromObject(guids),
                    ["connectionDepth"] = 0,
                };

                var ghGetInteraction = new AIInteractionToolCall
                {
                    Name = "gh_get",
                    Arguments = ghGetParams,
                    Agent = AIAgent.Assistant,
                };

                var ghGetCall = new AIToolCall();
                ghGetCall.Endpoint = "gh_get";
                ghGetCall.SkipMetricsValidation = true;
                ghGetCall.Body = AIBodyBuilder.Create()
                    .Add(ghGetInteraction)
                    .Build();

                var ghGetResult = await ghGetCall.Exec().ConfigureAwait(false);

                if (!ghGetResult.Success)
                {
                    Debug.WriteLine("[gh_smart_connect] gh_get call failed");
                    return null;
                }

                // Extract GhJSON from the tool result
                var toolResultInteraction = ghGetResult.Body?.Interactions?
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                return toolResultInteraction?.Result?["ghjson"]?.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_smart_connect] Error calling gh_get: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Calls the AI model to get connection suggestions based on GhJSON and purpose.
        /// </summary>
        /// <param name="provider">AI provider name.</param>
        /// <param name="model">AI model name.</param>
        /// <param name="ghjson">The GhJSON document string.</param>
        /// <param name="purpose">The user-specified connection purpose.</param>
        /// <returns>A tuple of (connections JArray, reasoning string, AI metrics, AI messages).</returns>
        private async Task<(JArray Connections, string Reasoning, Infrastructure.AICall.Metrics.AIMetrics Metrics, List<AIRuntimeMessage> Messages)> GetAiSuggestionsAsync(
            string provider,
            string model,
            string ghjson,
            string purpose)
        {
            try
            {
                var userPrompt = $"GhJSON document:\n```json\n{ghjson}\n```\n\nPurpose: {purpose}";

                var builder = AIBodyBuilder.Create()
                    .AddSystem(this.systemPrompt)
                    .AddUser(userPrompt)
                    .WithContextFilter("-*")
                    .WithToolFilter("-*");

                var requestBody = builder.Build();

                var request = new AIRequestCall();
                request.Initialize(
                    provider: provider,
                    model: model,
                    capability: AICapability.TextInput | AICapability.TextOutput,
                    endpoint: this.toolName,
                    body: requestBody);

                var aiResult = await request.Exec().ConfigureAwait(false);

                if (!aiResult.Success)
                {
                    Debug.WriteLine("[gh_smart_connect] AI request failed");
                    return (null, null, aiResult?.Metrics, aiResult?.Messages);
                }

                var responseText = aiResult.Body?.GetLastText() ?? string.Empty;
                Debug.WriteLine($"[gh_smart_connect] AI response: {responseText}");

                // Parse JSON from AI response (may be wrapped in markdown fences)
                var parsed = ParseAiResponse(responseText);

                return (parsed.Connections, parsed.Reasoning, aiResult.Metrics, aiResult.Messages);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_smart_connect] Error calling AI: {ex.Message}");
                return (null, null, null, null);
            }
        }

        /// <summary>
        /// Parses the AI response text to extract connections and reasoning.
        /// Handles both raw JSON and markdown-fenced JSON responses.
        /// </summary>
        /// <param name="responseText">The raw AI response text.</param>
        /// <returns>A tuple of (connections JArray, reasoning string).</returns>
        private static (JArray Connections, string Reasoning) ParseAiResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return (null, null);
            }

            try
            {
                // Try to extract JSON object from the response
                var match = JsonObjectRegex().Match(responseText);
                if (!match.Success)
                {
                    Debug.WriteLine("[gh_smart_connect] No JSON object found in AI response");
                    return (null, null);
                }

                var json = JObject.Parse(match.Value);
                var connections = json["connections"] as JArray;
                var reasoning = json["reasoning"]?.ToString();

                return (connections, reasoning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_smart_connect] Error parsing AI response: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// Calls the gh_connect tool to execute the suggested connections.
        /// </summary>
        /// <param name="connections">JArray of connection objects with sourceGuid, sourceParam, targetGuid, targetParam.</param>
        /// <returns>The raw connection result JObject, or null on failure.</returns>
        private async Task<JObject> ExecuteConnectionsAsync(JArray connections)
        {
            if (connections == null || connections.Count == 0)
            {
                return null;
            }

            try
            {
                var connectParams = new JObject
                {
                    ["connections"] = connections,
                };

                var connectInteraction = new AIInteractionToolCall
                {
                    Name = "gh_connect",
                    Arguments = connectParams,
                    Agent = AIAgent.Assistant,
                };

                var connectCall = new AIToolCall();
                connectCall.Endpoint = "gh_connect";
                connectCall.SkipMetricsValidation = true;
                connectCall.Body = AIBodyBuilder.Create()
                    .Add(connectInteraction)
                    .Build();

                var connectResult = await connectCall.Exec().ConfigureAwait(false);

                if (!connectResult.Success)
                {
                    Debug.WriteLine("[gh_smart_connect] gh_connect call failed");
                    return null;
                }

                // Extract result from the tool result interaction
                var toolResultInteraction = connectResult.Body?.Interactions?
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                return toolResultInteraction?.Result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_smart_connect] Error calling gh_connect: {ex.Message}");
                return null;
            }
        }
    }
}
