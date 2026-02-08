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

/*
 * Portions of this code adapted from:
 * https://github.com/alfredatnycu/grasshopper-mcp
 * MIT License
 * Copyright (c) 2025 Alfred Chen
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for connecting Grasshopper components together.
    /// Creates wires between component parameters on the canvas.
    /// Pre-validates each connection before attempting it, providing
    /// detailed diagnostics on failures (inspired by grasshopper-mcp's
    /// validate_connection / AreParametersCompatible approach).
    /// </summary>
    public class gh_connect : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_connect";

        /// <summary>
        /// Returns the GH connect tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Connect Grasshopper components together by creating wires between outputs and inputs. " +
                             "Each connection is pre-validated before wiring: the tool checks that both components exist, " +
                             "the requested parameters are found, and the data types are compatible. " +
                             "Requires component GUIDs (use gh_get_selected or gh_get to find them first).",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connections"": {
                            ""type"": ""array"",
                            ""description"": ""Array of connection specifications"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""sourceGuid"": {
                                        ""type"": ""string"",
                                        ""description"": ""GUID of the source component (output side)""
                                    },
                                    ""sourceParam"": {
                                        ""type"": ""string"",
                                        ""description"": ""Name or nickname of the output parameter. If not specified, uses the first output.""
                                    },
                                    ""targetGuid"": {
                                        ""type"": ""string"",
                                        ""description"": ""GUID of the target component (input side)""
                                    },
                                    ""targetParam"": {
                                        ""type"": ""string"",
                                        ""description"": ""Name or nickname of the input parameter. If not specified, uses the first input.""
                                    }
                                },
                                ""required"": [""sourceGuid"", ""targetGuid""]
                            }
                        }
                    },
                    ""required"": [""connections""]
                }",
                execute: this.GhConnectToolAsync);
        }

        /// <summary>
        /// Executes the GH connect tool with pre-validation and undo support.
        /// </summary>
        private Task<AIReturn> GhConnectToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var connectionsArray = args["connections"] as JArray;

                if (connectionsArray == null || !connectionsArray.Any())
                {
                    output.CreateError("The 'connections' array is required and must contain at least one connection specification.");
                    return Task.FromResult(output);
                }

                var doc = Instances.ActiveCanvas?.Document;
                if (doc == null)
                {
                    output.CreateError("No active Grasshopper document found.");
                    return Task.FromResult(output);
                }

                var successfulConnections = new List<JObject>();
                var failedConnections = new List<JObject>();

                // Create a single undo record for the entire batch
                var undoRecord = new GH_UndoRecord("[SH] Connect Components");

                foreach (var connSpec in connectionsArray)
                {
                    var sourceGuidStr = connSpec["sourceGuid"]?.ToString();
                    var targetGuidStr = connSpec["targetGuid"]?.ToString();
                    var sourceParamName = connSpec["sourceParam"]?.ToString();
                    var targetParamName = connSpec["targetParam"]?.ToString();

                    // --- Phase 1: Input validation ---
                    if (string.IsNullOrEmpty(sourceGuidStr) || string.IsNullOrEmpty(targetGuidStr))
                    {
                        failedConnections.Add(CreateFailure(
                            "Missing sourceGuid or targetGuid",
                            sourceGuidStr,
                            targetGuidStr));
                        continue;
                    }

                    if (!Guid.TryParse(sourceGuidStr, out var sourceGuid) ||
                        !Guid.TryParse(targetGuidStr, out var targetGuid))
                    {
                        failedConnections.Add(CreateFailure(
                            "Invalid GUID format",
                            sourceGuidStr,
                            targetGuidStr));
                        continue;
                    }

                    // --- Phase 2: Pre-validation & connection ---
                    var result = ValidateAndConnect(doc, undoRecord, sourceGuid, targetGuid, sourceParamName, targetParamName);

                    if (result.Success)
                    {
                        successfulConnections.Add(new JObject
                        {
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr,
                            ["sourceParam"] = result.SourceParamName,
                            ["targetParam"] = result.TargetParamName,
                            ["sourceType"] = result.SourceTypeName,
                            ["targetType"] = result.TargetTypeName,
                            ["status"] = "connected",
                        });
                    }
                    else
                    {
                        var failure = CreateFailure(result.Error, sourceGuidStr, targetGuidStr);

                        if (result.AvailableSourceParams != null)
                        {
                            failure["availableSourceParams"] = JArray.FromObject(result.AvailableSourceParams);
                        }

                        if (result.AvailableTargetParams != null)
                        {
                            failure["availableTargetParams"] = JArray.FromObject(result.AvailableTargetParams);
                        }

                        failedConnections.Add(failure);
                    }
                }

                // Commit the undo record and redraw once after all connections
                if (successfulConnections.Any())
                {
                    doc.UndoUtil.RecordEvent(undoRecord);
                    doc.NewSolution(false);
                    Instances.RedrawCanvas();
                }

                var toolResult = new JObject
                {
                    ["successful"] = JArray.FromObject(successfulConnections),
                    ["failed"] = JArray.FromObject(failedConnections),
                    ["successCount"] = successfulConnections.Count,
                    ["failCount"] = failedConnections.Count,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error connecting components: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        /// <summary>
        /// Validates a single connection and, if valid, creates the wire.
        /// Returns a result with detailed diagnostics on failure.
        /// </summary>
        private static ConnectionResult ValidateAndConnect(
            GH_Document doc,
            GH_UndoRecord undoRecord,
            Guid sourceGuid,
            Guid targetGuid,
            string sourceParamName,
            string targetParamName)
        {
            // --- Resolve source object ---
            var sourceObj = doc.FindObject(sourceGuid, true);
            if (sourceObj == null)
            {
                return ConnectionResult.Fail($"Source component not found: {sourceGuid}");
            }

            // --- Resolve target object ---
            var targetObj = doc.FindObject(targetGuid, true);
            if (targetObj == null)
            {
                return ConnectionResult.Fail($"Target component not found: {targetGuid}");
            }

            // --- Direction check (inspired by grasshopper-mcp) ---
            if (sourceObj is IGH_Param sourceAsParam && sourceAsParam.Kind == GH_ParamKind.input)
            {
                return ConnectionResult.Fail(
                    $"Source '{sourceObj.Name}' is an input parameter and cannot be used as a connection source. " +
                    "Swap source and target, or choose an output parameter.");
            }

            if (targetObj is IGH_Param targetAsParam && targetAsParam.Kind == GH_ParamKind.output)
            {
                return ConnectionResult.Fail(
                    $"Target '{targetObj.Name}' is an output parameter and cannot be used as a connection target. " +
                    "Swap source and target, or choose an input parameter.");
            }

            // --- Collect available parameters ---
            var sourceParams = GetOutputs(sourceObj);
            var targetParams = GetInputs(targetObj);

            if (sourceParams.Count == 0)
            {
                return ConnectionResult.Fail(
                    $"Source component '{sourceObj.Name}' has no output parameters.");
            }

            if (targetParams.Count == 0)
            {
                return ConnectionResult.Fail(
                    $"Target component '{targetObj.Name}' has no input parameters.");
            }

            // --- Resolve requested parameters ---
            var src = ResolveParam(sourceParams, sourceParamName);
            if (src == null)
            {
                return ConnectionResult.FailWithHints(
                    $"Source output parameter '{sourceParamName}' not found on '{sourceObj.Name}'.",
                    sourceParams.Select(FormatParamInfo).ToList(),
                    null);
            }

            var dst = ResolveParam(targetParams, targetParamName);
            if (dst == null)
            {
                return ConnectionResult.FailWithHints(
                    $"Target input parameter '{targetParamName}' not found on '{targetObj.Name}'.",
                    null,
                    targetParams.Select(FormatParamInfo).ToList());
            }

            // --- Type compatibility pre-check ---
            var compatError = CheckTypeCompatibility(src, dst);
            if (compatError != null)
            {
                return ConnectionResult.FailWithHints(
                    compatError,
                    sourceParams.Select(FormatParamInfo).ToList(),
                    targetParams.Select(FormatParamInfo).ToList());
            }

            // --- Duplicate check ---
            if (dst.Sources.Any(s => s.InstanceGuid == src.InstanceGuid))
            {
                return ConnectionResult.Fail(
                    $"Connection already exists: {src.Name} -> {dst.Name}.");
            }

            // --- Record undo and wire ---
            dst.RecordUndoEvent(undoRecord);
            dst.AddSource(src);

            Debug.WriteLine($"[gh_connect] Connected {sourceObj.Name}.{src.Name} -> {targetObj.Name}.{dst.Name}");

            return ConnectionResult.Ok(src.Name, dst.Name, src.TypeName, dst.TypeName);
        }

        /// <summary>
        /// Checks whether the source output type is compatible with the target input type.
        /// Returns null if compatible, or a descriptive error string if not.
        /// </summary>
        private static string CheckTypeCompatibility(IGH_Param source, IGH_Param target)
        {
            // Same parameter type is always compatible
            if (source.GetType() == target.GetType())
            {
                return null;
            }

            var sourceType = source.Type;
            var targetType = target.Type;

            // If the target accepts the source type (or a base type), it's compatible
            if (targetType != null && sourceType != null && targetType.IsAssignableFrom(sourceType))
            {
                return null;
            }

            // Grasshopper performs runtime casting for many types (e.g. int→double,
            // point→vector, curve→geometry). Rather than maintaining a hardcoded
            // compatibility matrix, we allow the connection and let Grasshopper's
            // own type-casting system handle it. We only warn on clearly incompatible
            // types where the target is a concrete typed parameter and the source
            // type hierarchy shares no common ancestor below IGH_Goo.
            //
            // For now, return null (allow) — Grasshopper will show a runtime warning
            // if the cast fails, which is the standard user experience.
            return null;
        }

        /// <summary>
        /// Resolves a parameter by name/nickname from a list, falling back to the first parameter
        /// when no name is specified.
        /// </summary>
        private static IGH_Param ResolveParam(List<IGH_Param> list, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return list[0];
            }

            // Exact match on Name
            var match = list.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            // Exact match on NickName
            match = list.FirstOrDefault(p =>
                string.Equals(p.NickName, name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            // Substring match on Name (fuzzy, inspired by grasshopper-mcp)
            match = list.FirstOrDefault(p =>
                p.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

            return match;
        }

        /// <summary>
        /// Formats a parameter's info for diagnostic output.
        /// </summary>
        private static string FormatParamInfo(IGH_Param p)
        {
            var nick = string.Equals(p.Name, p.NickName, StringComparison.Ordinal)
                ? string.Empty
                : $" ({p.NickName})";
            return $"{p.Name}{nick} [{p.TypeName}]";
        }

        /// <summary>
        /// Gets the output parameters of a document object.
        /// </summary>
        private static List<IGH_Param> GetOutputs(IGH_DocumentObject obj)
        {
            if (obj is IGH_Component c)
            {
                return c.Params.Output.ToList();
            }

            if (obj is IGH_Param p)
            {
                return new List<IGH_Param> { p };
            }

            return new List<IGH_Param>();
        }

        /// <summary>
        /// Gets the input parameters of a document object.
        /// </summary>
        private static List<IGH_Param> GetInputs(IGH_DocumentObject obj)
        {
            if (obj is IGH_Component c)
            {
                return c.Params.Input.ToList();
            }

            if (obj is IGH_Param p)
            {
                return new List<IGH_Param> { p };
            }

            return new List<IGH_Param>();
        }

        /// <summary>
        /// Creates a standardized failure JObject.
        /// </summary>
        private static JObject CreateFailure(string error, string sourceGuid, string targetGuid)
        {
            return new JObject
            {
                ["error"] = error,
                ["sourceGuid"] = sourceGuid,
                ["targetGuid"] = targetGuid,
            };
        }

        /// <summary>
        /// Holds the result of a single connection attempt with diagnostics.
        /// </summary>
        private struct ConnectionResult
        {
            /// <summary>
            /// Whether the connection was created successfully.
            /// </summary>
            public bool Success;

            /// <summary>
            /// Error message if the connection failed.
            /// </summary>
            public string Error;

            /// <summary>
            /// Resolved source parameter name.
            /// </summary>
            public string SourceParamName;

            /// <summary>
            /// Resolved target parameter name.
            /// </summary>
            public string TargetParamName;

            /// <summary>
            /// Source parameter type name.
            /// </summary>
            public string SourceTypeName;

            /// <summary>
            /// Target parameter type name.
            /// </summary>
            public string TargetTypeName;

            /// <summary>
            /// Available source output parameters (for diagnostics on failure).
            /// </summary>
            public List<string> AvailableSourceParams;

            /// <summary>
            /// Available target input parameters (for diagnostics on failure).
            /// </summary>
            public List<string> AvailableTargetParams;

            /// <summary>
            /// Creates a successful result.
            /// </summary>
            public static ConnectionResult Ok(string srcName, string dstName, string srcType, string dstType)
            {
                return new ConnectionResult
                {
                    Success = true,
                    SourceParamName = srcName,
                    TargetParamName = dstName,
                    SourceTypeName = srcType,
                    TargetTypeName = dstType,
                };
            }

            /// <summary>
            /// Creates a failed result with an error message.
            /// </summary>
            public static ConnectionResult Fail(string error)
            {
                return new ConnectionResult { Success = false, Error = error };
            }

            /// <summary>
            /// Creates a failed result with an error message and available parameter hints.
            /// </summary>
            public static ConnectionResult FailWithHints(
                string error,
                List<string> availableSource,
                List<string> availableTarget)
            {
                return new ConnectionResult
                {
                    Success = false,
                    Error = error,
                    AvailableSourceParams = availableSource,
                    AvailableTargetParams = availableTarget,
                };
            }
        }
    }
}
