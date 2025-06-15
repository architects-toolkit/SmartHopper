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
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;

namespace SmartHopper.Config.Managers
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
        private static bool _toolsDiscovered = false;

        /// <summary>
        /// Register a single tool
        /// </summary>
        /// <param name="tool">The tool to register</param>
        public static void RegisterTool(AITool tool)
        {
            Debug.WriteLine($"[AIToolManager] Registering tool: {tool.Name}");
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
        /// <param name="toolName">The name of the tool to execute</param>
        /// <param name="parameters">The parameters for the tool</param>
        /// <param name="extraParameters">Additional parameters to merge into the tool parameters</param>
        /// <returns>The result of the tool execution</returns>
        public static async Task<object> ExecuteTool(string toolName, JObject parameters, JObject extraParameters)
        {
            // Ensure tools are discovered
            DiscoverTools();

            Debug.WriteLine($"[AIToolManager] Executing tool: {toolName}");

            // Check if tool exists
            if (!_tools.ContainsKey(toolName))
            {
                Debug.WriteLine($"[AIToolManager] Tool not found: {toolName}");
                dynamic errorObj = new ExpandoObject();
                errorObj.success = false;
                errorObj.error = $"Tool '{toolName}' not found";
                return errorObj;
            }

            // Merge extra parameters into parameters
            if (extraParameters != null)
            {
                foreach (var prop in extraParameters.Properties())
                {
                    parameters[prop.Name] = prop.Value;
                }
            }

            try
            {
                // Execute the tool
                Debug.WriteLine($"[AIToolManager] Tool found, executing: {toolName}");
                var result = await _tools[toolName].Execute(parameters);
                Debug.WriteLine($"[AIToolManager] Tool execution complete: {toolName}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIToolManager] Error executing tool {toolName}: {ex.Message}");
                dynamic errorObj = new ExpandoObject();
                errorObj.success = false;
                errorObj.error = $"Error executing tool '{toolName}': {ex.Message}";
                return errorObj;
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

                // Find all types in the SmartHopper.Core.Grasshopper.Tools namespace
                var toolsNamespace = "SmartHopper.Core.Grasshopper.Tools";
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
                        Debug.WriteLine($"[AIToolManager] Creating instance of tool provider: {providerType.FullName}");
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

        /// <summary>
        /// Generate tool definitions for AI providers
        /// </summary>
        /// <returns>JSON string of tool definitions in OpenAI format</returns>
        public static string GenerateToolDefinitionsForAI()
        {
            // Ensure tools are discovered
            DiscoverTools();

            // Build JSON array of tool definitions
            var toolDefinitions = new JArray();

            foreach (var tool in _tools.Values)
            {
                var toolDef = new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JObject.Parse(tool.ParametersSchema)
                };

                toolDefinitions.Add(toolDef);
            }

            return toolDefinitions.ToString();
        }

        /// <summary>
        /// Generate human-readable list of available tools for documentation
        /// </summary>
        /// <returns>Markdown formatted string describing available tools</returns>
        public static string GenerateToolDocumentation()
        {
            // Ensure tools are discovered
            DiscoverTools();

            if (_tools.Count == 0)
                return "No tools available.";

            var docs = new List<string>
            {
                "# Available Tools\n"
            };

            foreach (var tool in _tools.Values)
            {
                docs.Add($"## {tool.Name}\n");
                docs.Add($"{tool.Description}\n");
                docs.Add("### Parameters\n");

                // Parse parameters schema
                try
                {
                    var schema = JObject.Parse(tool.ParametersSchema);
                    var properties = schema["properties"] as JObject;
                    var required = schema["required"] as JArray;

                    if (properties != null)
                    {
                        foreach (var prop in properties)
                        {
                            var name = prop.Key;
                            var details = prop.Value as JObject;
                            var type = details?["type"]?.ToString() ?? "any";
                            var description = details?["description"]?.ToString() ?? "";
                            var isRequired = required?.Contains(name) ?? false;

                            docs.Add($"- **{name}** ({type}){(isRequired ? " (required)" : "")}");
                            if (!string.IsNullOrEmpty(description))
                                docs.Add($"  - {description}");
                        }
                    }
                }
                catch
                {
                    docs.Add("Error parsing parameters schema.");
                }

                docs.Add("\n");
            }

            return string.Join("\n", docs);
        }
    }
}
