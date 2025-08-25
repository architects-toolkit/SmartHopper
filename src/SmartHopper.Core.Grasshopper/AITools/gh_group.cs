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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for grouping Grasshopper components by GUID.
    /// </summary>
    public class gh_group : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_group";
        /// <summary>
        /// Returns the gh_group AI tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Group Grasshopper components by GUID into a single Grasshopper group.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""List of component GUIDs to include in the group.""
                        },
                        ""groupName"": {
                            ""type"": ""string"",
                            ""description"": ""Optional name for the group.""
                        },
                        ""color"": {
                            ""type"": ""string"",
                            ""description"": ""Optional group color as ARGB 'A,R,G,B', RGB 'R,G,B', HTML hex '#RRGGBB', or known color name (e.g., 'Red'). Alpha defaults to 255 (100% opacity).""
                        }
                    },
                    ""required"": [""guids""]
                }",
                execute: this.GhGroupAsync
            );
        }

        private Task<AIReturn> GhGroupAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var guidStrings = toolInfo.Arguments["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var groupName = toolInfo.Arguments["groupName"]?.ToString();
                var colorStr = toolInfo.Arguments["color"]?.ToString();
                var validGuids = new List<Guid>();

                foreach (var s in guidStrings)
                {
                    if (Guid.TryParse(s, out var g))
                    {
                        validGuids.Add(g);
                    }
                }

                if (!validGuids.Any())
                {
                output.CreateError("No valid GUIDs provided for grouping.");
                return Task.FromResult(output);
                }

                GH_Group group = null;

                // Combine UI operations and result resolution in a single UI thread callback
                var tcs = new TaskCompletionSource<object>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    // Parse color if provided
                    var groupColor = System.Drawing.Color.Empty;
                    if (!string.IsNullOrEmpty(colorStr))
                    {
                        try
                        {
                            groupColor = StringConverter.StringToColor(colorStr);
                            Debug.WriteLine($"[gh_group] Group color set to {groupColor}");
                        }
                        catch
                        {
                            // Invalid color string, ignoring
                        }
                    }
                    else
                    {
                        groupColor = System.Drawing.Color.FromArgb(255, 0, 200, 0);
                        Debug.WriteLine("[gh_group] No color provided, using default color");
                    }

                    // Create group with undo support
                    group = GHDocumentUtils.GroupObjects(validGuids, groupName, groupColor) as GH_Group;

                    // Update UI
                    Instances.RedrawCanvas();

                    // Resolve task result
                    if (group != null)
                    {
                    var toolResult = new JObject
                    {
                        ["group"] = group.InstanceGuid.ToString(),
                        ["grouped"] = JArray.FromObject(validGuids.Select(g => g.ToString()))
                    };

                    var body = AIBodyBuilder.Create()
                        .AddToolResult(toolResult)
                        .Build();

                    output.CreateSuccess(body, toolCall);
                    }
                    else
                    {
                        output.CreateError("Failed to create group.");
                    }
                });

                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}
