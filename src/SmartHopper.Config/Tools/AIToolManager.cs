/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHopper.Config.Tools
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
        /// <returns>The result of the tool execution</returns>
        public static async Task<object> ExecuteTool(string toolName, JObject parameters)
        {
            // Ensure tools are discovered
            DiscoverTools();
            
            Debug.WriteLine($"[AIToolManager] Executing tool: {toolName}");
            
            // Check if tool exists
            if (!_tools.ContainsKey(toolName))
            {
                Debug.WriteLine($"[AIToolManager] Tool not found: {toolName}");
                return new { 
                    success = false, 
                    error = $"Tool '{toolName}' not found"
                };
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
                return new {
                    success = false,
                    error = $"Error executing tool '{toolName}': {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// Auto-discover tools from assemblies
        /// </summary>
        public static void DiscoverTools()
        {
            // Only discover once
            if (_toolsDiscovered)
                return;
                
            Debug.WriteLine("[AIToolManager] Starting tool discovery");
            
            try
            {
                // Find all types that implement IAIToolProvider
                // First try in SmartHopper.Core.Grasshopper assembly
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name.StartsWith("SmartHopper"))
                    .ToList();
                
                int toolCount = 0;
                
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        Debug.WriteLine($"[AIToolManager] Checking assembly for tool providers: {assembly.GetName().Name}");
                        
                        var toolProviderTypes = assembly.GetTypes()
                            .Where(t => typeof(IAIToolProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                            .ToList();
                        
                        foreach (var providerType in toolProviderTypes)
                        {
                            try
                            {
                                Debug.WriteLine($"[AIToolManager] Found tool provider: {providerType.Name}");
                                var provider = (IAIToolProvider)Activator.CreateInstance(providerType);
                                var tools = provider.GetTools();
                                
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
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIToolManager] Error processing assembly {assembly.GetName().Name}: {ex.Message}");
                    }
                }
                
                Debug.WriteLine($"[AIToolManager] Tool discovery complete. Registered {toolCount} tools from {assemblies.Count} assemblies");
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
                docs.Add("```json");
                docs.Add(tool.ParametersSchema);
                docs.Add("```\n");
            }
            
            return string.Join("\n", docs);
        }
    }
}
