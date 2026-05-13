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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.PutOperations;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for placing Grasshopper components from GhJSON format.
    /// </summary>
    public class gh_put : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_put";
        /// <summary>
        /// Returns the GH put tool.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Add new components to the canvas from GhJSON format. Use this to create component networks, add missing components, or build parametric definitions. The GhJSON must include component types, positions, and connections.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": { ""type"": ""string"", ""description"": ""GhJSON document string"" },
                        ""editMode"": { ""type"": ""boolean"", ""description"": ""When true, existing components on canvas will be replaced. User will be prompted for confirmation."" }
                    },
                    ""required"": [""ghjson""]
                }",
                execute: this.GhPutToolAsync);
        }

        private async Task<AIReturn> GhPutToolAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            string analysisMsg = null;
            try
            {
                // Local tool: do not require provider/model/finish_reason metrics
                toolCall.SkipMetricsValidation = true;

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var json = args["ghjson"]?.ToString() ?? string.Empty;
                var editMode = args["editMode"]?.ToObject<bool>() ?? false;

                GhJson.IsValid(json, out analysisMsg);
                var document = GhJson.FromJson(json);

                // Apply fixes to normalize AI-generated JSON
                var fixResult = GhJson.Fix(document);
                document = fixResult.Document;

                // In edit mode, check for existing components that match instanceGuids
                var existingComponents = new Dictionary<Guid, IGH_DocumentObject>();
                var existingPositions = new Dictionary<Guid, PointF>();
                var componentsToReplace = new List<Guid>();

                // Captured external connections: source/target component + parameter names
                var capturedConnections = new List<(Guid sourceGuid, string sourceParam, Guid targetGuid, string targetParam)>();

                if (editMode && document?.Components != null)
                {
                    foreach (var compProps in document.Components.Where(c => c.InstanceGuid.HasValue && c.InstanceGuid.Value != Guid.Empty))
                    {
                        var guid = compProps.InstanceGuid.Value;
                        var existing = CanvasAccess.FindInstance(guid);
                        if (existing != null)
                        {
                            existingComponents[guid] = existing;
                            existingPositions[guid] = existing.Attributes.Pivot;
                            componentsToReplace.Add(guid);
                        }
                    }

                    // Prompt user for confirmation for each component to replace
                    if (componentsToReplace.Count > 0)
                    {
                        var confirmedReplacements = new List<Guid>();

                        foreach (var guid in componentsToReplace)
                        {
                            var confirmTcs = new TaskCompletionSource<bool>();
                            var componentName = existingComponents.TryGetValue(guid, out var comp) ? comp.Name : "Unknown";

                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                try
                                {
                                    var message = $"Do you want to replace component '{componentName}' with the new definition'?\n\n" +
                                                  "Click 'Yes' to replace this component.\n" +
                                                  "Click 'No' to create a new component instead.";
                                    var result = StyledMessageDialog.ShowConfirmation(message, "Replace", guid);
                                    confirmTcs.SetResult(result);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gh_put] Error showing confirmation dialog: {ex.Message}");
                                    confirmTcs.SetResult(false);
                                }
                            });

                            var shouldReplace = await confirmTcs.Task.ConfigureAwait(false);

                            if (shouldReplace)
                            {
                                confirmedReplacements.Add(guid);
                                Debug.WriteLine($"[gh_put] User confirmed replacement of component '{componentName}' ({guid})");
                            }
                            else
                            {
                                Debug.WriteLine($"[gh_put] User chose to create new component instead of replacing '{componentName}' ({guid})");
                            }
                        }

                        // Update the lists to only include confirmed replacements
                        componentsToReplace.Clear();
                        componentsToReplace.AddRange(confirmedReplacements);

                        // Remove non-confirmed components from tracking dictionaries
                        var guidsToRemove = existingComponents.Keys.Except(confirmedReplacements).ToList();
                        foreach (var guid in guidsToRemove)
                        {
                            existingComponents.Remove(guid);
                            existingPositions.Remove(guid);
                        }

                        Debug.WriteLine($"[gh_put] Final replacement count: {componentsToReplace.Count}");

                        // Capture external connections for replaced components (on UI thread, before removal)
                        if (componentsToReplace.Count > 0)
                        {
                            var captureTcs = new TaskCompletionSource<bool>();
                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                try
                                {
                                    var allObjects = CanvasAccess.GetCurrentObjects();
                                    var replaceSet = new HashSet<Guid>(componentsToReplace);

                                    // Cache for mapping parameters to their owning document objects
                                    var ownerCache = new Dictionary<IGH_Param, IGH_DocumentObject>();

                                    IGH_DocumentObject FindOwner(IGH_Param param)
                                    {
                                        if (param == null)
                                        {
                                            return null;
                                        }

                                        if (ownerCache.TryGetValue(param, out var cached))
                                        {
                                            return cached;
                                        }

                                        // Stand-alone parameters appear directly in the active object list
                                        IGH_DocumentObject owner = allObjects.FirstOrDefault(o => ReferenceEquals(o, param));

                                        // Otherwise, look for a component that owns this parameter
                                        owner ??= allObjects
                                            .OfType<IGH_Component>()
                                            .FirstOrDefault(comp => comp.Params.Input.Contains(param) || comp.Params.Output.Contains(param));

                                        ownerCache[param] = owner;
                                        return owner;
                                    }

                                    var seen = new HashSet<(Guid, string, Guid, string)>();

                                    foreach (var guid in componentsToReplace)
                                    {
                                        if (!existingComponents.TryGetValue(guid, out var existing))
                                        {
                                            continue;
                                        }

                                        if (existing is IGH_Component comp)
                                        {
                                            // Outgoing connections: component -> external targets
                                            foreach (var outParam in comp.Params.Output)
                                            {
                                                var sourceParamName = outParam.NickName;
                                                foreach (var recipient in outParam.Recipients)
                                                {
                                                    var targetOwner = FindOwner(recipient);
                                                    if (targetOwner == null)
                                                    {
                                                        continue;
                                                    }

                                                    var targetGuid = targetOwner.InstanceGuid;

                                                    // Only keep external connections
                                                    if (replaceSet.Contains(targetGuid))
                                                    {
                                                        continue;
                                                    }

                                                    var key = (guid, sourceParamName, targetGuid, recipient.NickName);
                                                    if (seen.Add(key))
                                                    {
                                                        capturedConnections.Add((
                                                            sourceGuid: guid,
                                                            sourceParam: sourceParamName,
                                                            targetGuid: targetGuid,
                                                            targetParam: recipient.NickName));
                                                    }
                                                }
                                            }

                                            // Incoming connections: external sources -> component
                                            foreach (var inParam in comp.Params.Input)
                                            {
                                                var targetParamName = inParam.NickName;
                                                foreach (var source in inParam.Sources)
                                                {
                                                    var sourceOwner = FindOwner(source);
                                                    if (sourceOwner == null)
                                                    {
                                                        continue;
                                                    }

                                                    var sourceGuid = sourceOwner.InstanceGuid;

                                                    if (replaceSet.Contains(sourceGuid))
                                                    {
                                                        continue;
                                                    }

                                                    var key = (sourceGuid, source.NickName, guid, targetParamName);
                                                    if (seen.Add(key))
                                                    {
                                                        capturedConnections.Add((
                                                            sourceGuid: sourceGuid,
                                                            sourceParam: source.NickName,
                                                            targetGuid: guid,
                                                            targetParam: targetParamName));
                                                    }
                                                }
                                            }
                                        }
                                        else if (existing is IGH_Param param)
                                        {
                                            // Stand-alone parameter being replaced
                                            var thisGuid = param.InstanceGuid;

                                            // Sources -> this parameter
                                            foreach (var source in param.Sources)
                                            {
                                                var sourceOwner = FindOwner(source);
                                                if (sourceOwner == null)
                                                {
                                                    continue;
                                                }

                                                var sourceGuid2 = sourceOwner.InstanceGuid;

                                                if (replaceSet.Contains(sourceGuid2))
                                                {
                                                    continue;
                                                }

                                                var key = (sourceGuid2, source.NickName, thisGuid, param.NickName);
                                                if (seen.Add(key))
                                                {
                                                    capturedConnections.Add((
                                                        sourceGuid: sourceGuid2,
                                                        sourceParam: source.NickName,
                                                        targetGuid: thisGuid,
                                                        targetParam: param.NickName));
                                                }
                                            }

                                            // This parameter -> recipients
                                            foreach (var recipient in param.Recipients)
                                            {
                                                var targetOwner = FindOwner(recipient);
                                                if (targetOwner == null)
                                                {
                                                    continue;
                                                }

                                                var targetGuid = targetOwner.InstanceGuid;

                                                if (replaceSet.Contains(targetGuid))
                                                {
                                                    continue;
                                                }

                                                var key = (thisGuid, param.NickName, targetGuid, recipient.NickName);
                                                if (seen.Add(key))
                                                {
                                                    capturedConnections.Add((
                                                        sourceGuid: thisGuid,
                                                        sourceParam: param.NickName,
                                                        targetGuid: targetGuid,
                                                        targetParam: recipient.NickName));
                                                }
                                            }
                                        }
                                    }

                                    Debug.WriteLine($"[gh_put] Captured {capturedConnections.Count} external connections");
                                    captureTcs.SetResult(true);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gh_put] Error capturing connections: {ex.Message}");
                                    captureTcs.SetResult(false);
                                }
                            });

                            await captureTcs.Task.ConfigureAwait(false);
                        }
                    }
                }

                if (document?.Components == null || !document.Components.Any())
                {
                    var msg = analysisMsg ?? "JSON must contain a non-empty components array";
                    output.CreateError(msg);
                    return output;
                }

                // Put operation must run on UI thread.
                Debug.WriteLine("[gh_put] Putting document on canvas");
                PutResult putResult = null;
                var placeTcs = new TaskCompletionSource<bool>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var ghDoc = Instances.ActiveCanvas?.Document;

                        // Remove existing components that will be replaced
                        // Keep document enabled - disabling causes "object expired" errors
                        if (componentsToReplace.Count > 0 && ghDoc != null)
                        {
                            // Record undo event for component replacement
                            ghDoc.UndoUtil.RecordRemoveObjectEvent($"[SH] Replace {componentsToReplace.Count} component(s)", existingComponents.Values);

                            foreach (var guid in componentsToReplace)
                            {
                                if (existingComponents.TryGetValue(guid, out var existing))
                                {
                                    // IsolateObject cleans up connections before removal
                                    existing.IsolateObject();
                                    ghDoc.RemoveObject(existing, false);
                                    Debug.WriteLine($"[gh_put] Removed existing component '{existing.Name}' with GUID {guid}");
                                }
                            }
                        }

                        var putOptions = new PutOptions
                        {
                            // In edit mode we keep instance guids from the input GhJSON.
                            // Otherwise we regenerate to avoid accidental collisions.
                            RegenerateInstanceGuids = !editMode,
                            CreateConnections = true,
                            CreateGroups = true,
                            SelectPlacedObjects = true,
                            SkipInvalidComponents = true,
                            AutoOffset = !editMode,
                        };

                        putResult = GhJsonGrasshopper.Put(document, putOptions);

                        if (!putResult.Success)
                        {
                            throw new InvalidOperationException($"Put failed: {putResult.ErrorMessage}");
                        }

                        // Restore captured external connections
                        if (capturedConnections.Count > 0)
                        {
                            Debug.WriteLine("[gh_put] Restoring captured external connections");
                            int restored = 0;

                            foreach (var conn in capturedConnections)
                            {
                                try
                                {
                                    var success = ConnectByNickName(
                                        conn.sourceGuid,
                                        conn.targetGuid,
                                        conn.sourceParam,
                                        conn.targetParam);

                                    if (success)
                                    {
                                        restored++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gh_put] Error restoring connection {conn.sourceGuid}.{conn.sourceParam} -> {conn.targetGuid}.{conn.targetParam}: {ex.Message}");
                                }
                            }

                            Debug.WriteLine($"[gh_put] Restored {restored}/{capturedConnections.Count} external connections");
                        }

                        placeTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        placeTcs.SetException(ex);
                    }
                });
                await placeTcs.Task.ConfigureAwait(false);

                Debug.WriteLine("[gh_put] Placement complete");

                // Collect actual instanceGuids of placed components
                var placedGuids = putResult.PlacedObjects
                    .Select(o => o.InstanceGuid.ToString())
                    .ToList();

                var placedNames = putResult.PlacedObjects
                    .Select(o => o.Name)
                    .ToList();

                var toolResult = new JObject
                {
                    ["components"] = JArray.FromObject(placedNames),
                    ["instanceGuids"] = JArray.FromObject(placedGuids),
                    ["analysis"] = analysisMsg,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                Debug.WriteLine("[gh_put] Creating success output");
                output.CreateSuccess(body, toolCall);
                Debug.WriteLine("[gh_put] Returning from GhPutToolAsync");
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PutTools] Error in GhPutToolAsync: {ex.Message}");
                var combined = string.IsNullOrEmpty(analysisMsg)
                    ? ex.Message
                    : analysisMsg + "\nException: " + ex.Message;
                output.CreateError(combined);
                return output;
            }
        }
        /// <summary>
        /// Connects two components on the active canvas by matching parameter NickNames.
        /// Replaces the removed ConnectionBuilder.ConnectComponents() utility.
        /// </summary>
        /// <param name="sourceGuid">Instance GUID of the source component.</param>
        /// <param name="targetGuid">Instance GUID of the target component.</param>
        /// <param name="sourceParamName">NickName of the source output parameter.</param>
        /// <param name="targetParamName">NickName of the target input parameter.</param>
        /// <returns><c>true</c> if the connection was created successfully.</returns>
        private static bool ConnectByNickName(Guid sourceGuid, Guid targetGuid, string sourceParamName, string targetParamName)
        {
            var ghDoc = Instances.ActiveCanvas?.Document;
            if (ghDoc == null)
            {
                return false;
            }

            var sourceObj = ghDoc.FindObject(sourceGuid, true);
            var targetObj = ghDoc.FindObject(targetGuid, true);
            if (sourceObj == null || targetObj == null)
            {
                return false;
            }

            IGH_Param sourceParam = null;
            IGH_Param targetParam = null;

            // Resolve source output parameter
            if (sourceObj is IGH_Component sourceComp)
            {
                sourceParam = sourceComp.Params.Output
                    .FirstOrDefault(p => string.Equals(p.NickName, sourceParamName, StringComparison.OrdinalIgnoreCase));
            }
            else if (sourceObj is IGH_Param sp)
            {
                sourceParam = sp;
            }

            // Resolve target input parameter
            if (targetObj is IGH_Component targetComp)
            {
                targetParam = targetComp.Params.Input
                    .FirstOrDefault(p => string.Equals(p.NickName, targetParamName, StringComparison.OrdinalIgnoreCase));
            }
            else if (targetObj is IGH_Param tp)
            {
                targetParam = tp;
            }

            if (sourceParam == null || targetParam == null)
            {
                Debug.WriteLine($"[gh_put] ConnectByNickName: Could not resolve params - source '{sourceParamName}' on {sourceGuid}, target '{targetParamName}' on {targetGuid}");
                return false;
            }

            targetParam.AddSource(sourceParam);
            return true;
        }
    }
}
