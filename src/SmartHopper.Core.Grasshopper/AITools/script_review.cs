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
using GhJSON.Core.NameResolution;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.Serialization;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Components;
using SmartHopper.Core.Grasshopper.Utils.Constants;
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
    /// Provides the "script_review" AI tool for code reviewing script components.
    /// </summary>
    public partial class script_review : IAIToolProvider
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for detecting Python function definitions.
        /// </summary>
        [GeneratedRegex(@"^\s*def\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
        private static partial Regex PythonDefRegex();

        /// <summary>
        /// Regex pattern for detecting Python print statements.
        /// </summary>
        [GeneratedRegex(@"print\(", RegexOptions.IgnoreCase)]
        private static partial Regex PythonPrintRegex();

        /// <summary>
        /// Regex pattern for detecting C# RunScript method.
        /// </summary>
        [GeneratedRegex(@"void\s+RunScript", RegexOptions.IgnoreCase)]
        private static partial Regex CSharpRunScriptRegex();

        /// <summary>
        /// Regex pattern for detecting C# Console.WriteLine statements.
        /// </summary>
        [GeneratedRegex(@"Console\.WriteLine", RegexOptions.IgnoreCase)]
        private static partial Regex CSharpConsoleWriteLineRegex();

        /// <summary>
        /// Regex pattern for detecting VB.NET RunScript subroutine.
        /// </summary>
        [GeneratedRegex(@"sub\s+RunScript", RegexOptions.IgnoreCase)]
        private static partial Regex VBRunScriptRegex();

        /// <summary>
        /// Regex pattern for detecting VB.NET debug statements.
        /// </summary>
        [GeneratedRegex(@"Debug\.Print|Console\.WriteLine", RegexOptions.IgnoreCase)]
        private static partial Regex VBDebugRegex();

        #endregion

        /// <summary>
        /// System prompt template for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPromptTemplate = "You are a code review assistant for Grasshopper script components. Provide short feedback on the code focusing on identifying coding errors, potential bugs, and runtime issues. Return three sections: (1) what is this code for; (2) a list of potential issues and suggested fixes (if any); (3) a list of best practices that can be applied.";

        /// <summary>
        /// User prompt template for general review. Use <code> placeholder.
        /// </summary>
        private readonly string generalReviewPromptTemplate =
            "Perform a general review of the following script code:\n```\n<code>\n```\n\nPlease: (1) describe the main purpose; (2) detect potential bugs or incoherences; (3) suggest an improved code block.";

        /// <summary>
        /// User prompt template for question-based review. Use <question> and <code> placeholders.
        /// </summary>
        private readonly string questionReviewPromptTemplate =
            "SCRIPT CODE TO REVIEW:\n\n---\n\n<code>\n\n---\n\n" +
            "REVIEW QUESTION:\n\n---\n\n\"<question>\"\n\n---\n\n";

        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "script_review";

        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        /// <returns>An enumerable containing the single "script_review" tool definition.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Return a code review for the script component specified by its GUID. Example: script_review({ guid: '...', question: 'Are there performance issues?' }). See also: gh_get_by_guid, gh_get_selected.",
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
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput,
                mutatesCanvas: false,
                tags: new[] { "scripting", "script", "review", "read-only", "ghjson" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""review"": { ""type"": ""string"", ""description"": ""AI-generated code review."" }, ""warnings"": { ""type"": ""array"" }, ""errors"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        /// <summary>
        /// Executes the "script_review" tool: retrieves the component by GUID, runs coded checks, and obtains an AI-based review.
        /// </summary>
        /// <param name="toolCall">Tool invocation with provider/model context and arguments (expects a 'guid' field and optional 'question').</param>
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
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var guidStr = args["guid"]?.ToString() ?? throw new ArgumentException("Missing 'guid' parameter.");
                if (!Guid.TryParse(guidStr, out var scriptGuid))
                {
                    output.CreateError($"Invalid GUID: {guidStr}");
                    return output;
                }

                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var endpoint = this.toolName;
                var question = args["question"]?.ToString();
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                // Retrieve the script component from the current canvas
                var objects = CanvasAccess.GetCurrentObjects();
                var target = objects.FirstOrDefault(o => o.InstanceGuid == scriptGuid);
                if (target == null || !ScriptComponentReflection.IsScriptComponent(target))
                {
                    output.CreateError($"Script component with GUID {scriptGuid} not found.");
                    return output;
                }

                // Use GhJsonSerializer to extract component data (optional - provides structured data)
                // For review, we primarily need the script code, but serializer gives us full context
                string scriptCode = string.Empty;
                string language = "unknown";
                var componentData = new JObject();

                try
                {
                    // Extract using GhJsonGrasshopper for consistency
                    var activeTarget = target as IGH_ActiveObject;
                    if (activeTarget == null)
                    {
                        throw new InvalidOperationException("Script component does not implement IGH_ActiveObject.");
                    }

                    var componentsList = new List<IGH_ActiveObject> { activeTarget };
                    var document = GhJsonGrasshopper.Serialize(componentsList, SerializationOptions.Default);
                    var props = document.Components.FirstOrDefault();

                    if (props != null)
                    {
                        scriptCode = ExtractScriptCode(props) ?? string.Empty;
                        language = ScriptComponentRegistry.GetLanguageKey(props.ComponentGuid);

                        // Build component context for AI (optional rich context)
                        componentData["language"] = language;
                        componentData["inputCount"] = props.InputSettings?.Count ?? 0;
                        componentData["outputCount"] = props.OutputSettings?.Count ?? 0;
                        componentData["codeLines"] = scriptCode.Split('\n').Length;

                        Debug.WriteLine($"[script_review] Extracted via GhJsonSerializer: {language}, {scriptCode.Length} chars");
                    }
                    else
                    {
                        // Fallback to direct access
                        scriptCode = ScriptComponentReflection.GetScriptText(target);
                        language = ScriptComponentRegistry.GetLanguageKey((activeTarget as IGH_Component)?.ComponentGuid);
                        Debug.WriteLine($"[script_review] Using fallback extraction");
                    }
                }
                catch (Exception ex)
                {
                    // Fallback if serialization fails
                    scriptCode = ScriptComponentReflection.GetScriptText(target);
                    language = target is IGH_ActiveObject ao ? ScriptComponentRegistry.GetLanguageKey((ao as IGH_Component)?.ComponentGuid) : "unknown";
                    Debug.WriteLine($"[script_review] Serialization failed, using fallback: {ex.Message}");
                }

                // Validate we actually got script code
                if (string.IsNullOrWhiteSpace(scriptCode))
                {
                    output.CreateError($"Could not extract script code from component {scriptGuid}. The component may not be a supported script type (Python, C#, IronPython, VB).");
                    return output;
                }

                // Coded static checks by language
                var codedIssues = new List<string>();

                // Always check for TODO comments
                if (scriptCode.Contains("TODO", StringComparison.OrdinalIgnoreCase))
                    codedIssues.Add("Found TODO comments in script.");

                // Check line count
                var lineCount = scriptCode.Split('\n').Length;
                if (lineCount > 200)
                    codedIssues.Add($"Script has {lineCount} lines; consider refactoring into smaller methods.");

                // Language-specific debug checks
                if (PythonDefRegex().IsMatch(scriptCode)
                    || scriptCode.Contains("import ", StringComparison.OrdinalIgnoreCase))
                {
                    // Python/IronPython
                    var debugPy = PythonPrintRegex().Matches(scriptCode).Count;
                    if (debugPy > 0)
                    {
                        codedIssues.Add("Remove debug 'print' statements from Python script.");
                    }
                }
                else if (CSharpRunScriptRegex().IsMatch(scriptCode)
                         || scriptCode.Contains("using ", StringComparison.OrdinalIgnoreCase))
                {
                    // C#
                    var debugCs = CSharpConsoleWriteLineRegex().Matches(scriptCode).Count;
                    if (debugCs > 0)
                        codedIssues.Add("Remove debug 'Console.WriteLine' statements from C# script.");
                }
                else if (VBRunScriptRegex().IsMatch(scriptCode)
                         || scriptCode.Contains("imports ", StringComparison.OrdinalIgnoreCase))
                {
                    // VB.NET
                    var debugVb = VBDebugRegex().Matches(scriptCode).Count;
                    if (debugVb > 0)
                        codedIssues.Add("Remove debug statements (Debug.Print or Console.WriteLine) from VB script.");
                }
                else
                {
                    // Unknown language
                    codedIssues.Add("Warning: script language not recognized; static checks may be incomplete.");
                }

                // AI-based code review using AIRequestCall/AIReturn flow with immutable body
                var languageGuidance = ScriptCodeValidator.GetLanguageGuidance(language);
                var systemPrompt = this.systemPromptTemplate
                    + $"\n\nThe current script is written in '{language}'.\n\n{languageGuidance}";

                var builder = AIBodyBuilder.Create()
                    .WithContextFilter(contextFilter)
                    .AddSystem(systemPrompt);

                string userPrompt;
                if (string.IsNullOrWhiteSpace(question))
                {
                    userPrompt = this.generalReviewPromptTemplate.Replace("<code>", scriptCode);
                }
                else
                {
                    userPrompt = this.questionReviewPromptTemplate.Replace("<question>", question);
                    userPrompt = userPrompt.Replace("<code>", scriptCode);
                }

                builder.AddUser(userPrompt);
                var immutableBody = builder.Build();

                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: AICapability.TextInput | AICapability.TextOutput,
                    endpoint: endpoint,
                    body: immutableBody);

                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    // Propagate structured messages and metrics from AI call
                    output.CreateError("AI review request failed", toolCall, result.Metrics);
                    foreach (var msg in result.Messages.Where(m => m != null))
                    {
                        output.AddRuntimeMessage(msg.Severity, msg.Origin, msg.Message);
                    }

                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();

                // Success case
                var toolResult = new JObject();
                toolResult.Add("success", true);
                toolResult.Add("codedIssues", new JArray(codedIssues));
                toolResult.Add("aiReview", response);

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                var outImmutable = outBuilder.Build();
                output.CreateSuccess(outImmutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError(ex.Message);
                return output;
            }
        }


        private static string? ExtractScriptCode(GhJSON.Core.SchemaModels.GhJsonComponent component)
        {
            var ext = component?.ComponentState?.Extensions;
            if (ext == null)
            {
                return null;
            }

            var extensionKeys = new[]
            {
                ScriptComponentRegistry.GetExtensionKey("python"),
                ScriptComponentRegistry.GetExtensionKey("ironpython"),
                ScriptComponentRegistry.GetExtensionKey("c#"),
                ScriptComponentRegistry.GetExtensionKey("vb")
            };

            foreach (var key in extensionKeys)
            {
                if (!ext.TryGetValue(key, out var dataObj) || dataObj == null)
                {
                    continue;
                }

                if (dataObj is Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue(GhJsonExtensionKeys.CodeProperty, out var codeObj) && codeObj != null)
                    {
                        return codeObj.ToString();
                    }

                    if (dict.TryGetValue(GhJsonExtensionKeys.VBCodeProperty, out var vbObj) && vbObj is Dictionary<string, object> vbDict)
                    {
                        if (vbDict.TryGetValue(GhJsonExtensionKeys.VBScriptProperty, out var vbScript) && vbScript != null)
                        {
                            return vbScript.ToString();
                        }
                    }
                }
                else
                {
                    // Best-effort: extension payload might deserialize as JObject
                    var jo = dataObj as JObject;
                    var code = jo?[GhJsonExtensionKeys.CodeProperty]?.ToString();
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        return code;
                    }

                    var vbCode = jo?[GhJsonExtensionKeys.VBCodeProperty] as JObject;
                    var vbScript = vbCode?[GhJsonExtensionKeys.VBScriptProperty]?.ToString();
                    if (!string.IsNullOrWhiteSpace(vbScript))
                    {
                        return vbScript;
                    }
                }
            }

            return null;
        }
    }
}