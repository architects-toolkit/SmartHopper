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
using System.Reflection;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Central manager for AI tools that can be called from chat interfaces.
    /// Provides auto-discovery, registration, and execution of tools.
    /// </summary>
    public static class AIToolManager
    {
        // Dictionary to store all available tools
        private static readonly Dictionary<string, AITool> _tools = new Dictionary<string, AITool>();

        // Flag to track if tools have been discovered
        private static bool _toolsDiscovered;

        /// <summary>
        /// Register a single tool
        /// </summary>
        /// <param name="tool">The tool to register</param>
        public static void RegisterTool(AITool tool)
        {
            _tools[tool.Name] = tool;
        }

        /// <summary>
        /// Get all registered tools
        /// </summary>
        /// <returns>Dictionary of registered tools</returns>
        public static IReadOnlyDictionary<string, AITool> GetTools()
        {
            // Ensure tools are discovered
            DiscoverTools();
            return _tools;
        }

        /// <summary>
        /// Execute a tool with its parameters
        /// </summary>
        /// <param name="toolCall">The tool call to execute</param>
        /// <returns>The result of the tool execution</returns>
        public static async Task<AIReturn> ExecuteTool(AIToolCall toolCall)
        {
            // Ensure tools are discovered
            DiscoverTools();

            var toolInfo = toolCall.GetToolCall();

            Debug.WriteLine($"[AIToolManager] Executing tool: {toolInfo.Name}");

            var output = new AIReturn()
            {
                Request = toolCall,
            };

            // Validate tool call
            var (isValid, errors) = toolCall.IsValid();
            if (!isValid)
            {
                var reasonList = (errors ?? new List<AIRuntimeMessage>())
                    .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Message))
                    .Select(m => m.Message)
                    .ToList();
                var reasonText = reasonList.Count > 0 ? string.Join(" \n", reasonList) : "Tool call is invalid";
                Debug.WriteLine($"[AIToolManager] Tool call is invalid: {reasonText}");

                // Standardize as a tool error with structured messages for diagnostics/UI
                output.CreateToolError(reasonText, toolCall);
                if (errors != null && errors.Count > 0)
                {
                    output.Messages = errors;
                }

                return output;
            }

            try
            {
                // Execute the tool
                Debug.WriteLine($"[AIToolManager] Tool found, executing: {toolInfo.Name}");
                var result = await _tools[toolInfo.Name].Execute(toolCall);
                Debug.WriteLine($"[AIToolManager] Tool execution complete: {toolInfo.Name}");

                // Ensure tool result interactions carry the original tool call id/name/TurnId for provider schemas (e.g., OpenAI tool_call_id)
                try
                {
                    var results = result?.Body?.Interactions?
                        .OfType<SmartHopper.Infrastructure.AICall.Core.Interactions.AIInteractionToolResult>()
                        .ToList();
                    if (results != null && results.Count > 0)
                    {
                        foreach (var r in results)
                        {
                            if (string.IsNullOrWhiteSpace(r.Id)) r.Id = toolInfo.Id;
                            if (string.IsNullOrWhiteSpace(r.Name)) r.Name = toolInfo.Name;

                            // Propagate TurnId from tool call to tool result so they belong to the same turn
                            if (string.IsNullOrWhiteSpace(r.TurnId)) r.TurnId = toolInfo.TurnId;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIToolManager] Warning: failed to propagate tool call id/name/TurnId into result: {ex.Message}");
                }

                output.SetBody(result.Body);

                // Propagate tool execution messages so downstream components can surface them
                if (result?.Messages != null && result.Messages.Count > 0)
                {
                    output.Messages = result.Messages;
                }

                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIToolManager] Error executing tool {toolInfo.Name}: {ex.Message}");

                // Standardize as a tool error and add a structured message tagged with Tool origin
                output.CreateToolError($"Error executing tool '{toolInfo.Name}': {ex.Message}", toolCall);
                return output;
            }
        }

        /// <summary>
        /// Auto-discover tools from the SmartHopper.Core.Grasshopper/Tools directory
        /// </summary>
        public static void DiscoverTools()
        {
            // Only discover once
            if (_toolsDiscovered)
                return;

            Debug.WriteLine("[AIToolManager] Starting tool discovery");

            try
            {
                // For security reasons, restrict tool discovery to only SmartHopper.Core.Grasshopper/Tools
                // First, ensure the Core.Grasshopper assembly is loaded
                Assembly coreGrasshopperAssembly = null;
                try
                {
                    coreGrasshopperAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "SmartHopper.Core.Grasshopper");

                    if (coreGrasshopperAssembly == null)
                    {
                        Debug.WriteLine("[AIToolManager] Loading SmartHopper.Core.Grasshopper assembly");
                        coreGrasshopperAssembly = Assembly.Load("SmartHopper.Core.Grasshopper");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIToolManager] Error loading Core.Grasshopper assembly: {ex.Message}");
                    return;
                }

                if (coreGrasshopperAssembly == null)
                {
                    Debug.WriteLine("[AIToolManager] Could not find or load SmartHopper.Core.Grasshopper assembly");
                    return;
                }

                Debug.WriteLine($"[AIToolManager] Successfully loaded Core.Grasshopper assembly: {coreGrasshopperAssembly.GetName().Version}");

                // Find all types in the SmartHopper.Core.Grasshopper.AITools namespace
                var toolsNamespace = "SmartHopper.Core.Grasshopper.AITools";
                Debug.WriteLine($"[AIToolManager] Searching for tool providers in namespace: {toolsNamespace}");

                // Get all types in the Tools namespace
                var toolsTypes = coreGrasshopperAssembly.GetTypes()
                    .Where(t => t.Namespace == toolsNamespace)
                    .ToList();

                Debug.WriteLine($"[AIToolManager] Found {toolsTypes.Count} types in Tools namespace");

                // Filter to only those that implement IAIToolProvider
                var toolProviderTypes = toolsTypes
                    .Where(t => typeof(IAIToolProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                Debug.WriteLine($"[AIToolManager] Found {toolProviderTypes.Count} tool provider types");

                int toolCount = 0;
                foreach (var providerType in toolProviderTypes)
                {
                    try
                    {
                        var provider = (IAIToolProvider)Activator.CreateInstance(providerType);
                        var tools = provider.GetTools().ToList();

                        Debug.WriteLine($"[AIToolManager] Provider {providerType.Name} returned {tools.Count} tools");

                        foreach (var tool in tools)
                        {
                            RegisterTool(tool);
                            toolCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIToolManager] Error registering tools from {providerType.Name}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[AIToolManager] Tool discovery complete. Registered {toolCount} tools from {toolProviderTypes.Count} tool sets");
                _toolsDiscovered = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIToolManager] Error during tool discovery: {ex.Message}");
            }
        }
    }
}
