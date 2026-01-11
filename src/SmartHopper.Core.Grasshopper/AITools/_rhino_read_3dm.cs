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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Rhino.DocObjects;
using SmartHopper.Core.Grasshopper.Utils.Rhino;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for analyzing Rhino 3DM files.
    /// Reads and extracts metadata and object information from .3dm files.
    /// </summary>
    public class rhino_read_3dm : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "rhino_read_3dm";

        /// <summary>
        /// Returns the Rhino read 3DM tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Analyze a Rhino .3dm file and extract information about objects, layers, and file metadata. Returns summary statistics and object details. Use this to understand the contents of a 3DM file before processing.",

                // category: "Rhino",
                category: "NotTested",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filePath"": {
                            ""type"": ""string"",
                            ""description"": ""Full path to the .3dm file to analyze""
                        },
                        ""includeObjectDetails"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to include detailed information about each object. Default is false (summary only)."",
                            ""default"": false
                        },
                        ""maxObjects"": {
                            ""type"": ""integer"",
                            ""description"": ""Maximum number of objects to include in detailed output. Default is 100."",
                            ""default"": 100
                        },
                        ""objectTypeFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optional filter for object types to include. Available: Point, Curve, Surface, Brep, Mesh, Annotation, InstanceReference, TextDot, Hatch, Light, Grip, Phantom, ClipPlane""
                        }
                    },
                    ""required"": [""filePath""]
                }",
                execute: this.RhinoRead3dmToolAsync);
        }

        /// <summary>
        /// Executes the Rhino read 3DM tool.
        /// </summary>
        private Task<AIReturn> RhinoRead3dmToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var filePath = args["filePath"]?.ToString();
                var includeDetails = args["includeObjectDetails"]?.ToObject<bool>() ?? false;
                var maxObjects = args["maxObjects"]?.ToObject<int>() ?? 100;
                var typeFilterArray = args["objectTypeFilter"] as JArray;

                // Parse type filter
                HashSet<ObjectType> typeFilter = null;
                if (typeFilterArray != null && typeFilterArray.Any())
                {
                    typeFilter = typeFilterArray
                        .Select(typeStr => (success: Enum.TryParse<ObjectType>(typeStr.ToString(), true, out var objType), objType))
                        .Where(x => x.success)
                        .Select(x => x.objType)
                        .ToHashSet();
                }

                // Use utility to read 3DM file
                var toolResult = File3dmReader.Read3dmFile(filePath, includeDetails, maxObjects, typeFilter);

                if (toolResult == null)
                {
                    output.CreateError($"Failed to read 3DM file: {filePath}");
                    return Task.FromResult(output);
                }

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error reading 3DM file: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}
