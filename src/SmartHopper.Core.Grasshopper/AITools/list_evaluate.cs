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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
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
    /// Contains tools for list analysis and manipulation using AI.
    /// </summary>
    public class list_evaluate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "list_evaluate";

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
            $"This is my question: \"<question>\"\n\n" +
            $"Answer to the previous question with the following list:\n<list>\n\n";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Evaluates a list based on a natural language question",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Array of strings to evaluate (e.g., ['apple', 'banana', 'orange'])"" },
                        ""question"": { ""type"": ""string"", ""description"": ""The natural language question to answer about the list"" }
                    },
                    ""required"": [""list"", ""question""]
                }",
                execute: this.EvaluateList,
                requiredCapabilities: this.toolCapabilityRequirements);
        }

        /// <summary>
        /// Tool wrapper for the EvaluateList function.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider/model context and arguments.</param>
        /// <returns>The tool execution result envelope.</returns>
        private async Task<AIReturn> EvaluateList(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[ListTools] Running EvaluateList tool");

                // Extract parameters
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.toolName;
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string? question = args["question"]?.ToString();
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                if (args["list"] == null || string.IsNullOrEmpty(question))
                {
                    output.CreateError("Missing required parameters");
                    return output;
                }

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

                // Parse the boolean from the response
                var parsedResult = AIResponseParser.ParseBooleanFromResponse(response);

                if (parsedResult == null)
                {
                    output.CreateError($"The AI returned an invalid response:\n{result}");
                    return output;
                }

                // Success case
                var toolResult = new JObject();
                toolResult.Add("result", parsedResult.Value);

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListToolWrapper: {ex.Message}");

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
            var token = args["list"];
            if (token is JArray array)
            {
                return array.Select(t => t.ToString()).ToList();
            }

            var raw = token?.ToString();
            return AIResponseParser.ParseStringArrayFromResponse(raw);
        }
    }
}
