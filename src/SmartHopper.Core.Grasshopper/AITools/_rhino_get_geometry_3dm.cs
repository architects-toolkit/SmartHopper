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
    /// Tool provider for extracting geometry information from the active Rhino document.
    /// Retrieves detailed geometry data from selected or filtered objects.
    /// </summary>
    public class rhino_get_geometry : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "rhino_get_geometry";

        /// <summary>
        /// Returns the Rhino get geometry tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Extract detailed geometry information from objects in the active Rhino document. Can retrieve selected objects, objects by layer, or objects by type. Returns geometry properties, coordinates, and metadata.",

                // category: "Rhino",
                category: "NotTested",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filter"": {
                            ""type"": ""string"",
                            ""description"": ""Filter mode: 'selected' (default), 'all', 'layer', 'type'"",
                            ""enum"": [""selected"", ""all"", ""layer"", ""type""],
                            ""default"": ""selected""
                        },
                        ""layerName"": {
                            ""type"": ""string"",
                            ""description"": ""Layer name to filter by (required when filter='layer')""
                        },
                        ""objectType"": {
                            ""type"": ""string"",
                            ""description"": ""Object type to filter by (required when filter='type'). Options: Point, Curve, Surface, Brep, Mesh, Annotation, InstanceReference, TextDot, Hatch""
                        },
                        ""includeDetails"": {
                            ""type"": ""boolean"",
                            ""description"": ""Include detailed geometry data (vertices, control points, etc.). Default is false."",
                            ""default"": false
                        },
                        ""maxObjects"": {
                            ""type"": ""integer"",
                            ""description"": ""Maximum number of objects to return. Default is 50."",
                            ""default"": 50
                        }
                    }
                }",
                execute: this.RhinoGetGeometryToolAsync);
        }

        /// <summary>
        /// Executes the Rhino get geometry tool.
        /// </summary>
        private Task<AIReturn> RhinoGetGeometryToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var filter = args["filter"]?.ToString() ?? "selected";
                var layerName = args["layerName"]?.ToString();
                var objectTypeStr = args["objectType"]?.ToString();
                var includeDetails = args["includeDetails"]?.ToObject<bool>() ?? false;
                var maxObjects = args["maxObjects"]?.ToObject<int>() ?? 50;

                // Parse object type if specified
                ObjectType? objectType = null;
                if (!string.IsNullOrEmpty(objectTypeStr) &&
                    Enum.TryParse<ObjectType>(objectTypeStr, true, out var objType))
                {
                    objectType = objType;
                }

                // Use utility to get geometry
                var toolResult = DocumentGeometryExtractor.GetGeometry(filter, layerName, objectType, includeDetails, maxObjects);

                if (toolResult == null)
                {
                    output.CreateError($"Failed to retrieve geometry. Check filter parameters and ensure Rhino document is active.");
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
                output.CreateError($"Error retrieving geometry: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}
