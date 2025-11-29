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
using SmartHopper.Core.Grasshopper.Graph;
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

                // Captured document: serialized old components with connections (for merge)
                GrasshopperDocument capturedDocument = null;

                if (editMode && document?.Components != null)
                {
                    foreach (var compProps in document.Components)
                    {
                        if (compProps.InstanceGuid != Guid.Empty)
                        {
                            var existing = CanvasAccess.FindInstance(compProps.InstanceGuid);
                            if (existing != null)
                            {
                                existingComponents[compProps.InstanceGuid] = existing;
                                existingPositions[compProps.InstanceGuid] = existing.Attributes.Pivot;
                                componentsToReplace.Add(compProps.InstanceGuid);
                            }
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
                                    var message = $"Do you want to replace component '{componentName} with the new definition'?\n\n" +
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

                        // Capture components with depth=1 connections (on UI thread)
                        if (componentsToReplace.Count > 0)
                        {
                            var captureTcs = new TaskCompletionSource<GrasshopperDocument>();
                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                try
                                {
                                    // Get all canvas objects and serialize to get connection graph
                                    var allObjects = CanvasAccess.GetCurrentObjects();
                                    var serOptions = SerializationOptions.Standard;
                                    serOptions.IncludeMetadata = false;
                                    serOptions.IncludeGroups = false;

                                    var fullDoc = GhJsonSerializer.Serialize(allObjects, serOptions);

                                    // Build edges for depth expansion
                                    var idToGuidMap = fullDoc.GetIdToGuidMapping();
                                    var edges = fullDoc.Connections?
                                        .Select(c => c.TryResolveGuids(idToGuidMap, out var from, out var to)
                                            ? (from, to, valid: true)
                                            : (Guid.Empty, Guid.Empty, valid: false))
                                        .Where(e => e.valid)
                                        .Select(e => (e.from, e.to))
                                        ?? Enumerable.Empty<(Guid, Guid)>();

                                    // Expand to depth=1 (include directly connected components)
                                    var expandedGuids = ConnectionGraphUtils.ExpandByDepth(edges, componentsToReplace, depth: 1);

                                    // Serialize only the expanded components
                                    var expandedObjects = allObjects
                                        .Where(o => expandedGuids.Contains(o.InstanceGuid))
                                        .ToList();

                                    capturedDocument = GhJsonSerializer.Serialize(expandedObjects, serOptions);
                                    Debug.WriteLine($"[gh_put] Captured {capturedDocument.Components?.Count ?? 0} components, {capturedDocument.Connections?.Count ?? 0} connections");
                                    captureTcs.SetResult(capturedDocument);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gh_put] Error capturing connections: {ex.Message}");
                                    captureTcs.SetResult(null);
                                }
                            });
                            capturedDocument = await captureTcs.Task.ConfigureAwait(false);
                        }
                    }
                }

                if (document?.Components == null || !document.Components.Any())
                {
                    var msg = analysisMsg ?? "JSON must contain a non-empty components array";
                    output.CreateError(msg);
                    return output;
                }

                // Merge captured document into the incoming document (before deserialization)
                HashSet<Guid> externalComponentGuids = null;
                if (capturedDocument != null)
                {
                    var mergeResult = GhJsonMerger.Merge(document, capturedDocument);
                    externalComponentGuids = mergeResult.ExternalComponentGuids;
                    Debug.WriteLine($"[gh_put] Merged: +{mergeResult.ComponentsAdded} components, +{mergeResult.ConnectionsAdded} connections ({mergeResult.ConnectionsDuplicated} dupes)");
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
                    GH_Document ghDoc = null;
                    bool wasEnabled = true;

                    try
                    {
                        ghDoc = Instances.ActiveCanvas?.Document;

                        // Disable document to prevent solution recalculation during replacement
                        if (ghDoc != null && componentsToReplace.Count > 0)
                        {
                            wasEnabled = ghDoc.Enabled;
                            ghDoc.Enabled = false;
                            Debug.WriteLine("[gh_put] Disabled document to prevent solution during replacement");
                        }

                        // Remove existing components that will be replaced
                        if (componentsToReplace.Count > 0 && ghDoc != null)
                        {
                            // Record undo event for component replacement
                            ghDoc.UndoUtil.RecordRemoveObjectEvent($"[SH] Replace {componentsToReplace.Count} component(s)", existingComponents.Values);

                            foreach (var guid in componentsToReplace)
                            {
                                if (existingComponents.TryGetValue(guid, out var existing))
                                {
                                    ghDoc.RemoveObject(existing, false);
                                    Debug.WriteLine($"[gh_put] Removed existing component '{existing.Name}' with GUID {guid}");
                                }
                            }
                        }

                        // Use exact positions for replacement mode (skip offset calculation)
                        bool useExactPositions = componentsToReplace.Count > 0 && existingPositions.Count > 0;
                        placed = ComponentPlacer.PlaceComponents(result, useExactPositions: useExactPositions);

                        // Create all connections (including merged external connections)
                        Debug.WriteLine("[gh_put] Creating connections from merged GhJSON");
                        ConnectionManager.CreateConnections(result, externalComponentGuids);

                        Debug.WriteLine("[gh_put] Recreating groups");
                        GroupManager.CreateGroups(result);

                        placeTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        placeTcs.SetException(ex);
                    }
                    finally
                    {
                        // Re-enable document and schedule solution only if it was enabled before
                        if (ghDoc != null && wasEnabled && componentsToReplace.Count > 0)
                        {
                            ghDoc.Enabled = true;
                            ghDoc.NewSolution(false);
                            Debug.WriteLine("[gh_put] Re-enabled document and scheduled new solution");
                        }
                    }
                });
                await placeTcs.Task.ConfigureAwait(false);
                
                Debug.WriteLine("[gh_put] Placement complete");

                var toolResult = new JObject
                {
                    ["components"] = JArray.FromObject(placed),
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
