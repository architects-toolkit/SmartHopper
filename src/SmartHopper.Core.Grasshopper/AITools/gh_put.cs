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
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Serialization.Canvas;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
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

                GhJsonValidator.Validate(json, out analysisMsg);
                var document = GHJsonConverter.DeserializeFromJson(json, fixJson: true);

                // In edit mode, check for existing components that match instanceGuids
                var existingComponents = new Dictionary<Guid, IGH_DocumentObject>();
                var existingPositions = new Dictionary<Guid, PointF>();
                var componentsToReplace = new List<Guid>();

                // Captured external connections: source/target component + parameter names
                var capturedConnections = new List<(Guid sourceGuid, string sourceParam, Guid targetGuid, string targetParam)>();

                if (editMode && document?.Components != null)
                {
                    foreach (var compProps in document.Components.Where(c => c.InstanceGuid != Guid.Empty))
                    {
                        var existing = CanvasAccess.FindInstance(compProps.InstanceGuid);
                        if (existing != null)
                        {
                            existingComponents[compProps.InstanceGuid] = existing;
                            existingPositions[compProps.InstanceGuid] = existing.Attributes.Pivot;
                            componentsToReplace.Add(compProps.InstanceGuid);
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

                                    // Cache for mapping parameters to their owning document objects (component or stand-alone param)
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
                                            // Outgoing connections: component → external targets
                                            foreach (var output in comp.Params.Output)
                                            {
                                                var sourceParamName = output.NickName;
                                                foreach (var recipient in output.Recipients)
                                                {
                                                    var targetOwner = FindOwner(recipient);
                                                    if (targetOwner == null)
                                                    {
                                                        continue;
                                                    }

                                                    var targetGuid = targetOwner.InstanceGuid;

                                                    // Only keep external connections (one side in replaceSet, the other outside)
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

                                            // Incoming connections: external sources → component
                                            foreach (var input in comp.Params.Input)
                                            {
                                                var targetParamName = input.NickName;
                                                foreach (var source in input.Sources)
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

                                            // Sources → this parameter
                                            foreach (var source in param.Sources)
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

                                                var key = (sourceGuid, source.NickName, thisGuid, param.NickName);
                                                if (seen.Add(key))
                                                {
                                                    capturedConnections.Add((
                                                        sourceGuid: sourceGuid,
                                                        sourceParam: source.NickName,
                                                        targetGuid: thisGuid,
                                                        targetParam: param.NickName));
                                                }
                                            }

                                            // This parameter → recipients
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

                // Deserialize components on UI thread (required for parameter and attribute ops)
                Debug.WriteLine("[gh_put] Deserializing components");
                var options = DeserializationOptions.Standard;
                var tcs = new TaskCompletionSource<SmartHopper.Core.Grasshopper.Serialization.GhJson.DeserializationResult>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var res = GhJsonDeserializer.Deserialize(document, options);
                        tcs.SetResult(res);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
                var result = await tcs.Task.ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    output.CreateError($"Deserialization failed: {string.Join(", ", result.Errors)}");
                    return output;
                }

                // For replacement mode: restore original InstanceGuids before adding to canvas
                // This must happen BEFORE components are added to the document
                if (componentsToReplace.Count > 0)
                {
                    var guidRestored = GhJsonHelpers.RestoreInstanceGuids(result, componentsToReplace);
                    Debug.WriteLine($"[gh_put] Restored InstanceGuids for {guidRestored} replacement component(s)");
                }

                // Place components + create connections + groups on UI thread
                Debug.WriteLine("[gh_put] Placing components on canvas and creating connections/groups");
                object placed = null;
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

                        // Use exact positions for replacement mode (skip offset calculation)
                        bool useExactPositions = componentsToReplace.Count > 0 && existingPositions.Count > 0;
                        placed = ComponentPlacer.PlaceComponents(result, useExactPositions: useExactPositions);

                        // Create connections from GhJSON
                        Debug.WriteLine("[gh_put] Creating connections from GhJSON");
                        ConnectionManager.CreateConnections(result);

                        // Restore captured external connections
                        if (capturedConnections.Count > 0)
                        {
                            Debug.WriteLine("[gh_put] Restoring captured external connections");
                            int restored = 0;

                            foreach (var conn in capturedConnections)
                            {
                                try
                                {
                                    var success = ConnectionBuilder.ConnectComponents(
                                        sourceGuid: conn.sourceGuid,
                                        targetGuid: conn.targetGuid,
                                        sourceParamName: conn.sourceParam,
                                        targetParamName: conn.targetParam,
                                        redraw: false);

                                    if (success)
                                    {
                                        restored++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gh_put] Error restoring connection {conn.sourceGuid}.{conn.sourceParam} → {conn.targetGuid}.{conn.targetParam}: {ex.Message}");
                                }
                            }

                            Debug.WriteLine($"[gh_put] Restored {restored}/{capturedConnections.Count} external connections");
                        }

                        Debug.WriteLine("[gh_put] Recreating groups");
                        GroupManager.CreateGroups(result);

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
                var placedGuids = result.Components
                    .Select(c => c.InstanceGuid.ToString())
                    .ToList();

                var toolResult = new JObject
                {
                    ["components"] = JArray.FromObject(placed),
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
    }
}
