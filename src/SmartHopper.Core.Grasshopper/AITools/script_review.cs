/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides the "script_review" AI tool for code reviewing script components.
    /// </summary>
    public class script_review : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "script_review";
        
        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Return a code review for the script component specified by its GUID.",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guid"": {
                            ""type"": ""string"",
                            ""description"": ""Instance GUID of the component to review.""
                        },
                        ""question"": {
                            ""type"": ""string"",
                            ""description"": ""Optional user question or focus for the review.""
                        }
                    },
                    ""required"": [""guid""]
                }",
                execute: this.ScriptReviewToolAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput
            );
        }

        /// <summary>
        /// Executes the "script_review" tool: retrieves the component by GUID, runs coded checks, and obtains an AI-based review.
        /// </summary>
        /// <param name="parameters">JSON object with "guid" field.</param>
        /// <returns>Result containing success flag, codedIssues array, and aiReview string, or error details.</returns>
        private async Task<AIReturn> ScriptReviewToolAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Parse and validate parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();;
                var guidStr = toolInfo.Arguments["guid"]?.ToString() ?? throw new ArgumentException("Missing 'guid' parameter.");
                if (!Guid.TryParse(guidStr, out var scriptGuid))
                {
                    output.CreateError($"Invalid GUID: {guidStr}");
                    return output;
                }
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var endpoint = this.toolName;
                var question = toolInfo.Arguments["question"]?.ToString();
                string? contextFilter = toolInfo.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                // Retrieve the script component from the current canvas
                var objects = GHCanvasUtils.GetCurrentObjects();
                var target = objects.FirstOrDefault(o => o.InstanceGuid == scriptGuid) as IScriptComponent;
                if (target == null)
                {
                    output.CreateError($"Script component with GUID {scriptGuid} not found.");
                    return output;
                }

                var scriptCode = target.Text ?? string.Empty;

                // Coded static checks by language
                var codedIssues = new List<string>();

                // Always check for TODO comments
                if (scriptCode.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0)
                    codedIssues.Add("Found TODO comments in script.");

                // Check line count
                var lineCount = scriptCode.Split('\n').Length;
                if (lineCount > 200)
                    codedIssues.Add($"Script has {lineCount} lines; consider refactoring into smaller methods.");

                // Language-specific debug checks
                if (Regex.IsMatch(scriptCode, @"^\s*def\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                    || scriptCode.IndexOf("import ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Python/IronPython
                    var debugPy = Regex.Matches(scriptCode, @"print\(", RegexOptions.IgnoreCase).Count;
                    if (debugPy > 0)
                    {
                        codedIssues.Add("Remove debug 'print' statements from Python script.");
                    }
                }
                else if (Regex.IsMatch(scriptCode, @"void\s+RunScript", RegexOptions.IgnoreCase)
                         || scriptCode.IndexOf("using ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // C#
                    var debugCs = Regex.Matches(scriptCode, @"Console\.WriteLine", RegexOptions.IgnoreCase).Count;
                    if (debugCs > 0)
                        codedIssues.Add("Remove debug 'Console.WriteLine' statements from C# script.");
                }
                else if (Regex.IsMatch(scriptCode, @"sub\s+RunScript", RegexOptions.IgnoreCase)
                         || scriptCode.IndexOf("imports ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // VB.NET
                    var debugVb = Regex.Matches(scriptCode, @"Debug\.Print|Console\.WriteLine", RegexOptions.IgnoreCase).Count;
                    if (debugVb > 0)
                        codedIssues.Add("Remove debug statements (Debug.Print or Console.WriteLine) from VB script.");
                }
                else
                {
                    // Unknown language
                    codedIssues.Add("Warning: script language not recognized; static checks may be incomplete.");
                }

                // AI-based code review using AIRequestCall/AIReturn flow
                var requestBody = new AIBody
                {
                    ContextFilter = contextFilter,
                };
                requestBody.AddInteraction(AIAgent.System, "You are a code review assistant. Provide concise feedback on the code.");

                string userPrompt;
                if (string.IsNullOrWhiteSpace(question))
                {
                    userPrompt = $"Perform a general review of the following script code:\n```\n{scriptCode}\n```\n\nPlease: (1) describe the main purpose; (2) detect potential bugs or incoherences; (3) suggest an improved code block.";
                }
                else
                {
                    userPrompt = $"Review the following script code with respect to this question: \"{question}\"\n```\n{scriptCode}\n```";
                }
                requestBody.AddInteraction(AIAgent.User, userPrompt);

                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: AICapability.TextInput | AICapability.TextOutput,
                    endpoint: endpoint,
                    body: requestBody);

                var result = await request.Exec().ConfigureAwait(false);
                if (!result.Success)
                {
                    output.CreateError(result.ErrorMessage ?? "AI request failed");
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();

                // Success case
                var toolResult = new JObject();
                toolResult.Add("success", true);
                toolResult.Add("codedIssues", new JArray(codedIssues));
                toolResult.Add("aiReview", response);

                var toolBody = new AIBody();
                toolBody.AddInteractionToolResult(toolResult, result.Metrics, result.Messages);

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
