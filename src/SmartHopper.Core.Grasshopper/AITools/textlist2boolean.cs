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
using System.Threading.Tasks;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for list analysis and manipulation using AI.
    /// </summary>
    public class textlist2boolean : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "textlist2boolean";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a list analyzer. Your task is to analyze a list of items and return a boolean value indicating whether the list matches the given criteria.\n\n" +
            "The list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\n" +
            "Mainly you will base your answers on the item itself, unless the user asks for something regarding the position of items in the list.\n\n" +
            "Respond with TRUE or FALSE, nothing else.";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <question> and <list> placeholders.
        /// </summary>
        private readonly string userPrompt =
            "LIST TO EVALUATE:\n\n---\n\n<list>\n\n---\n\n" +
            "QUESTION TO ANSWER:\n\n---\n\n\"<question>\"\n\n---\n\n" +
            "Answer TRUE or FALSE based on the list above, nothing else.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Evaluates a list based on a natural language question with optional fallback value. Example: textlist2boolean({ list: ['steel', 'concrete', 'timber'], question: 'Are all items structural materials?', fallback: false }).",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Array of strings to evaluate (e.g., ['apple', 'banana', 'orange'])"" },
                        ""question"": { ""type"": ""string"", ""description"": ""The natural language question to answer about the list"" },
                        ""fallback"": { ""type"": ""boolean"", ""description"": ""Optional fallback value to use when AI response cannot be parsed as true/false. If not provided, the result will be null for unparsable responses"" }
                    },
                    ""required"": [""list"", ""question""]
                }",
                execute: this.TextList2Boolean,
                requiredCapabilities: this.toolCapabilityRequirements,
                mutatesCanvas: false,
                tags: new[] { "text", "list", "boolean", "data-processing", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""result"": { ""type"": ""boolean"", ""description"": ""Boolean evaluation result or fallback value."" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        /// <summary>
        /// Tool wrapper for the TextList2Boolean function.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider/model context and arguments.</param>
        /// <returns>The tool execution result envelope.</returns>
        private async Task<AIReturn> TextList2Boolean(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[ListTools] Running TextList2Boolean tool");

                // Extract parameters
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.toolName;
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                string? question = args["question"]?.ToString();

                // Parse fallback as boolean using centralized helper
                string? fallbackStr = args["fallback"]?.ToString();
                bool? fallback = StringConverter.StringToBoolean(fallbackStr);
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                if (args["list"] == null || string.IsNullOrEmpty(question))
                {
                    output.CreateError("Missing required parameters");
                    return output;
                }

                Debug.WriteLine($"[ListTools.TextList2Boolean] Fallback value: '{fallback?.ToString() ?? "null"}'");

                // Normalize list input
                var items = NormalizeListInput(toolInfo);

                // Convert to GH_String list
                var ghStringList = items.Select(s => new GH_String(s)).ToList();

                string itemsJsonDict = AIResponseParser.ConcatenateItemsToJson(ghStringList);

                // Prepare the AI request
                var userPrompt = this.userPrompt;
                userPrompt = userPrompt.Replace("<question>", question);
                userPrompt = userPrompt.Replace("<list>", itemsJsonDict);

                // Initiate immutable AIBody
                var requestBody = AIBodyBuilder.Create()
                    .AddSystem(this.systemPrompt)
                    .AddUser(userPrompt)
                    .WithContextFilter(contextFilter)
                    .Build();

                // Initiate AIRequestCall
                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.toolCapabilityRequirements,
                    endpoint: endpoint,
                    body: requestBody);

                // Execute the AIRequestCall
                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    // Propagate structured messages from AI call
                    output.Messages = result.Messages;
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                Debug.WriteLine($"[ListTools.TextList2Boolean] AI response: '{response}'");

                // Centralized parse + fallback resolution (shared with batch path).
                var toolResult = BooleanResultResolver.BuildToolResult(response, fallback);
                Debug.WriteLine($"[ListTools.TextList2Boolean] Resolved: {toolResult}");

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in TextList2Boolean: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Normalizes the 'list' parameter into a list of strings, parsing malformed input.
        /// </summary>
        /// <param name="toolCall">The tool interaction containing the raw 'list' argument.</param>
        /// <returns>A list of string items parsed from the input argument.</returns>
        private static List<string> NormalizeListInput(AIInteractionToolCall toolCall)
        {
            var args = toolCall.Arguments ?? new JObject();
            return StringListResultResolver.ParseOrEmpty(args["list"]);
        }
    }
}