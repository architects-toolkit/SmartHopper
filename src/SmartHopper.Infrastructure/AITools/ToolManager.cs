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
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;
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

        // Lock to protect _tools and _toolsDiscovered from concurrent access. AIToolManager is a
        // static manager that may be used by multiple components and tests in parallel.
        private static readonly object _toolsLock = new object();

        // Flag to track if tools have been discovered
        private static bool _toolsDiscovered;

        /// <summary>
        /// Resets the tool manager to a clean state. For test use only.
        /// </summary>
        internal static void ResetTools()
        {
            lock (_toolsLock)
            {
                _tools.Clear();
                _toolsDiscovered = false;
            }
        }

        /// <summary>
        /// Adds or updates a tool in the registry without taking a lock.
        /// Must only be called while holding _toolsLock.
        /// </summary>
        /// <param name="tool">The tool to register</param>
        private static void RegisterToolCore(AITool tool)
        {
            _tools[tool.Name] = tool;
        }

        /// <summary>
        /// Register a single tool
        /// </summary>
        /// <param name="tool">The tool to register</param>
        public static void RegisterTool(AITool tool)
        {
            lock (_toolsLock)
            {
                RegisterToolCore(tool);
            }
        }

        /// <summary>
        /// Get all registered tools
        /// </summary>
        /// <returns>Dictionary of registered tools</returns>
        public static IReadOnlyDictionary<string, AITool> GetTools()
        {
            lock (_toolsLock)
            {
                // Ensure tools are discovered
                DiscoverTools();
                return new Dictionary<string, AITool>(_tools);
            }
        }

        /// <summary>
        /// Execute a tool with its parameters
        /// </summary>
        /// <param name="toolCall">The tool call to execute</param>
        /// <returns>The result of the tool execution</returns>
        public static async Task<AIReturn> ExecuteTool(AIToolCall toolCall)
        {
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
                var reasonList = (errors ?? new List<SHRuntimeMessage>())
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

            // Ensure tools are discovered
            DiscoverTools();
            
            // Resolve the target tool under the lock, then execute
            // outside the lock so async work does not block other callers.
            AITool tool;
            lock (_toolsLock)
            {
                if (!_tools.TryGetValue(toolInfo.Name, out tool))
                {
                    Debug.WriteLine($"[AIToolManager] Tool not found: {toolInfo.Name}");
                    output.CreateToolError($"Tool '{toolInfo.Name}' is not registered.", toolCall);
                    return output;
                }
            }

            // Normalize a null arguments object to an empty JObject when the tool schema has no
            // required parameters. ToolJsonSchemaValidator validates this case but cannot return the
            // normalized instance because IValidator<T> is side-effect free, so the normalization
            // is applied at the execution boundary where the value is actually consumed.
            if (toolInfo.Arguments == null && tool.GetRequiredParameters().Count == 0)
            {
                var normalized = toolInfo with { Arguments = new JObject() };
                var newBody = toolCall.Body.WithReplaced(toolInfo, normalized);
                if (!ReferenceEquals(newBody, toolCall.Body))
                {
                    toolCall.Body = newBody;
                    toolInfo = normalized;
                    Debug.WriteLine($"[AIToolManager] Normalized null arguments to empty JObject for tool '{toolInfo.Name}'");
                }
            }

            try
            {
                // Execute the tool
                Debug.WriteLine($"[AIToolManager] Tool found, executing: {toolInfo.Name}");

                // Extract cancellation token if available on the toolCall
                var cancellationToken = toolCall.CancellationToken;
                cancellationToken.ThrowIfCancellationRequested();

                var result = await tool.Execute(toolCall).ConfigureAwait(false);
                Debug.WriteLine($"[AIToolManager] Tool execution complete: {toolInfo.Name}");

                // Ensure tool result interactions carry the original tool call id/name/TurnId for provider schemas (e.g., OpenAI tool_call_id)
                try
                {
                    var originalBody = result?.Body;
                    if (originalBody != null)
                    {
                        var correctedInteractions = new List<SmartHopper.ProviderSdk.AICall.Core.Interactions.IAIInteraction>(originalBody.Interactions);
                        bool changed = false;
                        for (int i = 0; i < correctedInteractions.Count; i++)
                        {
                            if (correctedInteractions[i] is SmartHopper.ProviderSdk.AICall.Core.Interactions.AIInteractionToolResult r)
                            {
                                var id = string.IsNullOrWhiteSpace(r.Id) ? toolInfo.Id : r.Id;
                                var name = string.IsNullOrWhiteSpace(r.Name) ? toolInfo.Name : r.Name;
                                var turnId = string.IsNullOrWhiteSpace(r.TurnId) ? toolInfo.TurnId : r.TurnId;
                                if (id != r.Id || name != r.Name || string.IsNullOrWhiteSpace(r.TurnId))
                                {
                                    correctedInteractions[i] = r with
                                    {
                                        Id = id,
                                        Name = name,
                                        Agent = SmartHopper.ProviderSdk.AICall.Core.Base.AIAgent.ToolResult,
                                        TurnId = turnId,
                                    };
                                    changed = true;
                                }
                            }
                        }

                        if (changed)
                        {
                            var correctedBody = new SmartHopper.ProviderSdk.AICall.Core.Interactions.AIBody(
                                correctedInteractions,
                                originalBody.ToolFilter,
                                originalBody.ContextFilter,
                                originalBody.JsonOutputSchema,
                                originalBody.InteractionsNew);
                            result.SetBody(correctedBody);
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
            lock (_toolsLock)
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
                                RegisterToolCore(tool);
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
}
