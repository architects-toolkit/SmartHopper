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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Messaging;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.AITools;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for generating Grasshopper definitions from natural language prompts.
    /// Uses autonomous AI orchestration with available tools for intelligent component selection and GhJSON generation.
    /// </summary>
    public class gh_generate : IAIToolProvider
    {
        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_generate",
                description: "Generate Grasshopper definitions from natural language descriptions using autonomous AI orchestration. Returns JSON with status (succeed/fail) and result (GhJSON or clarification request). Pass the GhJSON to the 'gh_put' tool to load it into the Grasshopper canvas.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language description of what you want to create with Grasshopper components""
                        }
                    },
                    ""required"": [""prompt""]
                }",
                execute: this.GhGenerateToolAsync
            );
        }

        /// <summary>
        /// Executes the gh_generate tool using autonomous AI orchestration.
        /// AI decides when and how to use available tools to generate GhJSON from natural language prompts.
        /// </summary>
        private async Task<object> GhGenerateToolAsync(JObject parameters)
        {
            try
            {
                var userPrompt = parameters["prompt"]?.ToString();
                if (string.IsNullOrWhiteSpace(userPrompt))
                {
                    return new { status = "fail", result = new { error = "Prompt is required" } };
                }

                var providerName = parameters["provider"]?.ToString() ?? string.Empty;
                var modelName = parameters["model"]?.ToString() ?? string.Empty;
                var contextProviderFilter = parameters["contextProviderFilter"]?.ToString() ?? string.Empty;
                var contextKeyFilter = parameters["contextKeyFilter"]?.ToString() ?? string.Empty;

                Debug.WriteLine($"[gh_generate] Starting autonomous AI generation for prompt: {userPrompt}");

                // Use autonomous AI orchestration with continuous conversation
                Debug.WriteLine($"[gh_generate] Starting generation with continuous conversation");
                
                var result = await this.GenerateWithAutonomousAI(
                    userPrompt,
                    providerName,
                    modelName,
                    contextProviderFilter,
                    contextKeyFilter);
                
                Debug.WriteLine($"[gh_generate] Generation completed with status: {result.status}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error: {ex.Message}");
                return new { status = "fail", result = new { error = ex.Message } };
            }
        }

        /// <summary>
        /// Generates GhJSON using autonomous AI orchestration with available tools.
        /// </summary>
        private async Task<dynamic> GenerateWithAutonomousAI(string userPrompt, string providerName, string modelName, 
            string contextProviderFilter, string contextKeyFilter)
        {
            try
            {
                Debug.WriteLine($"[gh_generate] Starting autonomous AI generation with continuous conversation");
                
                // Get system prompt with tool descriptions
                var systemPrompt = this.GetAutonomousSystemPrompt();
                var baseUserMessage = $"User Request: {userPrompt}\n\nPlease analyze this request and generate a complete Grasshopper definition using the available tools.\n\nIMPORTANT: When you provide the final GhJSON response, return ONLY the formatted JSON, nothing else. Do not wrap it in markdown code blocks or add any explanatory text.";
                
                // Initialize conversation with system and user messages using ChatMessageModel
                const int maxTurns = 20;
                var userMessageWithTurnInfo = $"{baseUserMessage}\n\n**IMPORTANT: You have a maximum of {maxTurns-2} conversation turns to complete this task. Use your tools efficiently!**";
                
                var messages = new List<ChatMessageModel>
                {
                    new ChatMessageModel
                    {
                        Author = "system",
                        Body = systemPrompt,
                        Time = DateTime.Now,
                        Inbound = false,
                        Read = true,
                    },
                    new ChatMessageModel
                    {
                        Author = "user",
                        Body = userMessageWithTurnInfo,
                        Time = DateTime.Now,
                        Inbound = true,
                        Read = true,
                    },
                };

                Debug.WriteLine($"[gh_generate] System prompt length: {systemPrompt.Length}");
                Debug.WriteLine($"[gh_generate] User message: {userMessageWithTurnInfo}");

                // Multi-turn conversation loop
                for (int turn = 1; turn <= maxTurns; turn++)
                {
                    Debug.WriteLine($"[gh_generate] Turn {turn} - Sending request to AI");

                    // Get AI response with tool filter to expose relevant tools
                    var aiResponse = await AIUtils.GetResponse(
                        providerName,
                        modelName,
                        messages,
                        "", // no JSON schema
                        "", // no endpoint
                        "ComponentsRetrieval", // tool filter
                        contextProviderFilter,
                        contextKeyFilter
                    );

                    var content = aiResponse.Response ?? "";

                    Debug.WriteLine($"[gh_generate] Turn {turn} - AI Response Length: {content.Length}");
                    Debug.WriteLine($"[gh_generate] Turn {turn} - Tool Calls: {aiResponse.ToolCalls?.Count ?? 0}");
                    Debug.WriteLine($"[gh_generate] Turn {turn} - Finish Reason: {aiResponse.FinishReason}");

                    // Check if AI made tool calls
                    if (aiResponse.ToolCalls != null && aiResponse.ToolCalls.Any())
                    {
                        Debug.WriteLine($"[gh_generate] Turn {turn} - Processing {aiResponse.ToolCalls.Count} tool calls");

                        // Add the assistant message with tool calls to the conversation
                        // This is required for proper API message flow
                        var assistantMessage = new ChatMessageModel
                        {
                            Author = "assistant",
                            Body = content,
                            Time = DateTime.Now,
                            Inbound = false,
                            Read = true,
                            ToolCalls = aiResponse.ToolCalls.ToList() // Copy tool calls to message
                        };
                        messages.Add(assistantMessage);
                        
                        // Execute each tool call and add results to conversation
                        foreach (var toolCall in aiResponse.ToolCalls)
                        {
                            Debug.WriteLine($"[gh_generate] Executing tool: {toolCall.Name} with args: {toolCall.Arguments}");
                            
                            try
                            {
                                // Parse tool arguments
                                var toolArgs = JObject.Parse(toolCall.Arguments);
                                
                                // Execute the tool
                                var toolResult = await AIToolManager.ExecuteTool(toolCall.Name, toolArgs, new JObject());
                                
                                // Convert result to JSON string
                                var resultJson = JsonConvert.SerializeObject(toolResult, Formatting.Indented);
                                
                                Debug.WriteLine($"[gh_generate] Tool {toolCall.Name} result length: {resultJson.Length}");
                                
                                // Add tool result to conversation with proper tool_call_id linking
                                var toolMessage = new ChatMessageModel
                                {
                                    Author = "tool",
                                    Body = resultJson,
                                    Time = DateTime.Now,
                                    Inbound = false,
                                    Read = true,
                                    ToolCalls = new List<AIToolCall> { toolCall } // Link back to original tool call
                                };
                                messages.Add(toolMessage);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[gh_generate] Error executing tool {toolCall.Name}: {ex.Message}");
                                
                                // Add error as tool result
                                var errorResult = new { error = $"Failed to execute {toolCall.Name}: {ex.Message}" };
                                var errorJson = JsonConvert.SerializeObject(errorResult);
                                
                                var errorMessage = new ChatMessageModel
                                {
                                    Author = "tool",
                                    Body = errorJson,
                                    Time = DateTime.Now,
                                    Inbound = false,
                                    Read = true,
                                    ToolCalls = new List<AIToolCall> { toolCall } // Link back to original tool call
                                };
                                messages.Add(errorMessage);
                            }
                        }
                        
                        // Continue to next turn to let AI process tool results
                        continue;
                    }
                    
                    // Check if finish reason indicates tool calls but no tool calls were parsed
                    // This can happen with some providers - continue conversation to let AI retry
                    if (aiResponse.FinishReason == "tool_calls")
                    {
                        Debug.WriteLine($"[gh_generate] Turn {turn} - Finish reason is tool_calls but no tool calls found, continuing conversation");
                        
                        // Add the assistant message and continue - let AI orchestration handle this
                        var assistantMessage = new ChatMessageModel
                        {
                            Author = "assistant",
                            Body = content,
                            Time = DateTime.Now,
                            Inbound = false,
                            Read = true
                        };
                        messages.Add(assistantMessage);
                        continue;
                    }
                    
                    // Only treat as final response when finish reason is "stop" or "length"
                    if (aiResponse.FinishReason != "stop" && aiResponse.FinishReason != "length")
                    {
                        Debug.WriteLine($"[gh_generate] Turn {turn} - Finish reason '{aiResponse.FinishReason}' is not 'stop' or 'length', continuing conversation");
                        
                        // Add the assistant message and continue
                        var assistantMessage = new ChatMessageModel
                        {
                            Author = "assistant",
                            Body = content,
                            Time = DateTime.Now,
                            Inbound = false,
                            Read = true
                        };
                        messages.Add(assistantMessage);
                        continue;
                    }
                    
                    // Finish reason is "stop" or "length" - AI provided final response (may be truncated)
                    Debug.WriteLine($"[gh_generate] Turn {turn} - Final response received (finish reason: {aiResponse.FinishReason})");
                    Debug.WriteLine($"[gh_generate] Turn {turn} - AI Response Content: {content}");
                    
                    if (string.IsNullOrEmpty(content))
                    {
                        return new { status = "fail", result = new { error = "Empty response from AI" } };
                    }
                    
                    // Process the final response - check if it contains GhJSON
                    var extractedJson = this.ExtractGhJsonFromResponse(content);
                    if (!string.IsNullOrWhiteSpace(extractedJson))
                    {
                        Debug.WriteLine($"[gh_generate] Turn {turn} - Extracted JSON length: {extractedJson.Length}");
                        
                        // Validate the extracted GhJSON
                        var validationResult = this.ValidateGhJson(extractedJson);
                        
                        if (validationResult.isValid)
                        {
                            Debug.WriteLine($"[gh_generate] Turn {turn} - Validation successful, returning GhJSON");
                            return new { status = "succeed", result = extractedJson };
                        }
                        else
                        {
                            // Extract only errors for correction (exclude warnings and info)
                            var errorMessages = this.ExtractErrorsOnly(validationResult.errorMessage);
                            
                            if (!string.IsNullOrEmpty(errorMessages))
                            {
                                Debug.WriteLine($"[gh_generate] Turn {turn} - Validation failed, adding error correction message");
                                
                                // Add error correction message to continue the conversation
                                var errorCorrectionMessage = new ChatMessageModel
                                {
                                    Author = "user",
                                    Body = $"The GhJSON you provided has validation errors. Please fix these specific issues and provide a corrected GhJSON:\n\n{errorMessages}\n\nRemember to return ONLY the corrected JSON, nothing else.",
                                    Time = DateTime.Now,
                                    Inbound = true,
                                    Read = true,
                                };
                                messages.Add(errorCorrectionMessage);
                                
                                Debug.WriteLine($"[gh_generate] Turn {turn} - Error correction message added, continuing conversation");
                                continue; // Continue the conversation loop
                            }
                            else
                            {
                                Debug.WriteLine($"[gh_generate] Turn {turn} - Validation failed but no specific errors to correct");
                                return new { status = "fail", result = new { error = validationResult.errorMessage } };
                            }
                        }
                    }
                }
                
                // If we get here, we exceeded max turns
                Debug.WriteLine($"[gh_generate] Exceeded maximum conversation turns ({maxTurns})");
                return new { status = "fail", result = new { error = $"Exceeded maximum conversation turns ({maxTurns})" } };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error in autonomous AI generation: {ex.Message}");
                return new { status = "fail", result = new { error = ex.Message } };
            }
        }

        /// <summary>
        /// Gets the autonomous system prompt with instructions and tool availability.
        /// </summary>
        private string GetAutonomousSystemPrompt()
        {
            return $@"You are an expert Grasshopper 3D parametric design assistant with access to specialized tools. Your task is to analyze user requests and autonomously generate complete Grasshopper definitions in GhJSON format.

## AVAILABLE TOOLS:
1. **gh_list_categories** - List all available component categories
   Parameters: filter (optional), includeSubcategories (optional)
   
2. **gh_list_components** - List components from specific categories  
   Parameters: categoryFilter (array), nameFilter (optional), includeDetails (array), maxResults (optional)
   Use includeDetails to control what info is returned: [""name"", ""description"", ""inputs"", ""outputs""] are the most relevant details for GhJSON generation.

## YOUR WORKFLOW:
1. **Understand the Request**: Analyze what the user wants to create
2. **Explore Categories**: Use gh_list_categories to find relevant component categories
3. **Find Components**: Use gh_list_components strategically with filters to find the right components
4. **Generate GhJSON**: Create a complete, valid GhJSON definition

## GhJSON REQUIREMENTS:
- Use consecutive integer IDs (1, 2, 3...) for instanceGuid, NOT actual GUIDs
- Structure: {{""components"": [...], ""connections"": [...]}}
- Each component needs strictly only: {{""instanceGuid"": ""1"", ""name"": ""Number Slider""}}
- Connections need strictly only: {{""from"": {{""instanceId"": ""1"", ""paramName"": ""Number""}}, ""to"": {{""instanceId"": ""3"", ""paramName"": ""A""}}}} where instanceId refers to the instanceGuid of the component

## GhJSON TEMPLATES FOR COMMON TASKS:

**Simple Math (Add two numbers):**
```json
{{
  ""components"": [
    {{""instanceGuid"": ""1"", ""name"": ""Number Slider""}},
    {{""instanceGuid"": ""2"", ""name"": ""Number Slider""}},
    {{""instanceGuid"": ""3"", ""name"": ""Addition""}}
  ],
  ""connections"": [
    {{""from"": {{""instanceId"": ""1"", ""paramName"": ""Number""}}, ""to"": {{""instanceId"": ""3"", ""paramName"": ""A""}}}},
    {{""from"": {{""instanceId"": ""2"", ""paramName"": ""Number""}}, ""to"": {{""instanceId"": ""3"", ""paramName"": ""B""}}}}
  ]
}}
```

**Simple Curve (Circle):**
```json
{{
  ""components"": [
    {{""instanceGuid"": ""1"", ""name"": ""Point""}},
    {{""instanceGuid"": ""2"", ""name"": ""Number Slider""}},
    {{""instanceGuid"": ""3"", ""name"": ""Circle""}}
  ],
  ""connections"": [
    {{""from"": {{""instanceId"": ""1"", ""paramName"": ""Point""}}, ""to"": {{""instanceId"": ""3"", ""paramName"": ""Center""}}}},
    {{""from"": {{""instanceId"": ""2"", ""paramName"": ""Number""}}, ""to"": {{""instanceId"": ""3"", ""paramName"": ""Radius""}}}}
  ]
}}
```

## RESPONSE FORMAT:
If you need clarification, respond with JSON: {{""clarificationNeeded"": ""Your detailed question here""}}
If you can generate the definition, respond with valid GhJSON using consecutive IDs.

## DECISION MAKING:
- Ask for clarification ONLY if the request is genuinely ambiguous
- Use tools strategically to minimize token usage
- Prioritize common, well-documented Grasshopper components
- Ensure proper component connections for functional definitions";
        }

        /// <summary>
        /// Validates GhJSON using GHJsonAnalyzer and returns structured validation result.
        /// </summary>
        private (bool isValid, string errorMessage) ValidateGhJson(string ghjson)
        {
            try
            {
                Debug.WriteLine($"[gh_generate] Raw GhJSON being validated: {ghjson?.Substring(0, Math.Min(100, ghjson?.Length ?? 0))}...");

                // Validate the GhJSON structure
                var result = GHJsonLocal.Validate(ghjson, out var errorMessage);

                // Debug.WriteLine($"[gh_generate] Validation result: {result}, ErrorMessage: {errorMessage?.Substring(0, Math.Min(200, errorMessage?.Length ?? 0))}...");
                Debug.WriteLine($"[gh_generate] Validation result: {result}, ErrorMessage: {errorMessage}...");

                return (result, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Validation exception: {ex.Message}");
                return (false, $"GhJSON validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts only error messages from validation output, excluding warnings and info messages.
        /// Parses the structured validation output that has "Errors:", "Warnings:", and "Information:" sections.
        /// </summary>
        private string ExtractErrorsOnly(string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(validationMessage))
                return string.Empty;

            var lines = validationMessage.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var errorLines = new List<string>();
            bool inErrorsSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Check for section headings
                if (trimmedLine.Equals("Errors:", StringComparison.OrdinalIgnoreCase))
                {
                    inErrorsSection = true;
                    continue; // Skip the heading itself
                }
                else if (trimmedLine.Equals("Warnings:", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.Equals("Information:", StringComparison.OrdinalIgnoreCase))
                {
                    inErrorsSection = false;
                    continue;
                }
                
                // If we're in the errors section, collect the content
                if (inErrorsSection && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    errorLines.Add(trimmedLine);
                }
            }

            return string.Join("\n", errorLines);
        }

        /// <summary>
        /// Extracts GhJSON content from AI response, handling markdown code blocks.
        /// </summary>
        private string ExtractGhJsonFromResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;

            // Try to find JSON in markdown code blocks
            var jsonBlockStart = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonBlockStart >= 0)
            {
                var contentStart = response.IndexOf('\n', jsonBlockStart) + 1;
                var jsonBlockEnd = response.IndexOf("```", contentStart);
                if (jsonBlockEnd > contentStart)
                {
                    return response.Substring(contentStart, jsonBlockEnd - contentStart).Trim();
                }
            }

            // Try to find any code block
            var codeBlockStart = response.IndexOf("```");
            if (codeBlockStart >= 0)
            {
                var contentStart = response.IndexOf('\n', codeBlockStart) + 1;
                var codeBlockEnd = response.IndexOf("```", contentStart);
                if (codeBlockEnd > contentStart)
                {
                    var content = response.Substring(contentStart, codeBlockEnd - contentStart).Trim();
                    // Check if it looks like JSON
                    if (content.StartsWith("{") && content.EndsWith("}"))
                    {
                        return content;
                    }
                }
            }

            // Try to find raw JSON (starts with { and ends with })
            var firstBrace = response.IndexOf('{');
            var lastBrace = response.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                var possibleJson = response.Substring(firstBrace, lastBrace - firstBrace + 1);
                try
                {
                    // Quick validation that it's parseable JSON
                    JObject.Parse(possibleJson);
                    return possibleJson;
                }
                catch
                {
                    // Not valid JSON
                }
            }

            return null;
        }
    }
}
