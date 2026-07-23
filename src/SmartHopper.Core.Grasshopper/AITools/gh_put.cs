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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.ConnectionOperations;
using GhJSON.Grasshopper.PutOperations;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

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
                description: "Add new components to the canvas from GhJSON format. Use this to create component networks, add missing components, or build parametric definitions. The GhJSON must include component types, positions, and connections. Component-specific state (e.g. Number Slider values under componentState.extensions['gh.numberslider'].value using the format 'current<min~max>', Panel text under componentState.extensions['gh.panel'].text) is preserved. Example: gh_put({ ghjson: '...' }) or gh_put({ ghjson: 'C:/path/to/file.ghjson' }). See also: gh_get, script_generate_and_place_on_canvas.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": { ""type"": ""string"", ""description"": ""GhJSON document string, or an absolute file path to a .ghjson file containing the document."" },
                        ""editMode"": { ""type"": ""boolean"", ""description"": ""When true, existing components on canvas will be replaced. User will be prompted for confirmation."" },
                        ""autoOffset"": { ""type"": ""boolean"", ""default"": true, ""description"": ""When true, newly placed components are offset on the canvas so they do not overlap existing objects. In edit mode this defaults to false."" }
                    },
                    ""required"": [""ghjson""]
                }",
                execute: this.GhPutToolAsync,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "ghjson" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""components"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Names of the placed or replaced components."" }, ""instanceGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Instance GUIDs of the placed or replaced components."" }, ""analysis"": { ""type"": [""string"", ""null""], ""description"": ""Validation, error, or warning summary. Null when nothing notable happened."" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
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
                var args = toolInfo.GetArgumentsOrEmpty();
                var json = ExtractGhJsonString(args["ghjson"]);
                var editMode = args["editMode"]?.ToObject<bool>() ?? false;
                var autoOffset = args["autoOffset"]?.ToObject<bool>() ?? !editMode;

                GhJson.IsValid(json, out analysisMsg);
                var document = GhJson.FromJson(json);

                // Apply fixes to normalize AI-generated JSON
                var fixResult = GhJson.Fix(document);
                document = fixResult.Document;

                // Remove any components that are currently protected (enabled MCP server or wired to it)
                // from the incoming document so they are never modified or re-placed.
                var protectedGuids = CanvasProtection.GetProtectedInstanceGuids();
                var protectedPutGuids = new List<Guid>();

                if (protectedGuids.Count > 0 && document?.Components != null)
                {
                    protectedPutGuids = document.Components
                        .Where(c => c.InstanceGuid.HasValue && protectedGuids.Contains(c.InstanceGuid.Value))
                        .Select(c => c.InstanceGuid.Value)
                        .ToList();

                    if (protectedPutGuids.Count > 0)
                    {
                        var filteredComponents = document.Components
                            .Where(c => !(c.InstanceGuid.HasValue && protectedGuids.Contains(c.InstanceGuid.Value)))
                            .ToList();

                        var remainingIds = new HashSet<int>(filteredComponents.Where(c => c.Id.HasValue).Select(c => c.Id.Value));

                        var filteredConnections = document.Connections?.Where(conn =>
                            remainingIds.Contains(conn.From.Id) && remainingIds.Contains(conn.To.Id)).ToList();

                        var filteredGroups = document.Groups?.Select(g => new GhJsonGroup
                        {
                            Id = g.Id,
                            InstanceGuid = g.InstanceGuid,
                            Name = g.Name,
                            Color = g.Color,
                            Members = g.Members?.Where(m => remainingIds.Contains(m)).ToList() ?? new List<int>(),
                        }).Where(g => g.Members.Count > 0).ToList();

                        document = new GhJsonDocument(
                            document.Schema,
                            document.Metadata,
                            filteredComponents,
                            filteredConnections,
                            filteredGroups);
                    }
                }

                // In edit mode, check for existing components that match instanceGuids
                var existingComponents = new Dictionary<Guid, IGH_DocumentObject>();
                var existingPositions = new Dictionary<Guid, PointF>();
                var componentsToReplace = new List<Guid>();
                HashSet<Guid> unchangedGuids = null;

                // Captured external connections: source/target component + parameter names
                var capturedConnections = new List<ConnectionInfo>();

                if (editMode && document?.Components != null)
                {
                    // Step 1: Pre-compare components with canvas.
                    // Collect all incoming components with an instanceGuid that exists on canvas,
                    // then batch-serialize via GhJsonGrasshopper.GetByGuids for efficiency.
                    var incomingWithGuid = document.Components
                        .Where(c => c.InstanceGuid.HasValue && c.InstanceGuid.Value != Guid.Empty)
                        .ToList();

                    foreach (var compProps in incomingWithGuid)
                    {
                        var guid = compProps.InstanceGuid.Value;
                        var existing = CanvasAccess.FindInstance(guid);
                        if (existing != null)
                        {
                            existingComponents[guid] = existing;
                            existingPositions[guid] = existing.Attributes.Pivot;
                        }
                    }

                    if (existingComponents.Count > 0)
                    {
                        var guidsToCompare = existingComponents.Keys.ToList();
                        var existingDoc = GhJsonGrasshopper.GetByGuids(guidsToCompare);

                        foreach (var compProps in incomingWithGuid)
                        {
                            var guid = compProps.InstanceGuid.Value;
                            if (!existingComponents.ContainsKey(guid))
                            {
                                continue;
                            }

                            var existingComp = existingDoc.Components
                                .FirstOrDefault(c => c.InstanceGuid == guid);

                            if (existingComp != null && ComponentsAreEqual(compProps, existingComp))
                            {
                                Debug.WriteLine($"[gh_put] Component '{existingComponents[guid].Name}' ({guid}) is unchanged, skipping replacement");
                            }
                            else
                            {
                                componentsToReplace.Add(guid);
                                Debug.WriteLine($"[gh_put] Component '{existingComponents[guid].Name}' ({guid}) has changes, will prompt for replacement");
                            }
                        }
                    }

                    // Step 2: Prompt user for confirmation for each modified component
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

                        // Capture unchanged GUIDs BEFORE mutating existingComponents so Step 3
                        // can still filter out unchanged components correctly.
                        unchangedGuids = existingComponents.Keys.Except(confirmedReplacements).ToHashSet();

                        // Remove non-confirmed components from tracking dictionaries
                        foreach (var guid in unchangedGuids)
                        {
                            existingComponents.Remove(guid);
                            existingPositions.Remove(guid);
                        }

                        Debug.WriteLine($"[gh_put] Final replacement count: {componentsToReplace.Count}");

                        // If no replacements were needed, all existing components are unchanged.
                        // Ensure unchangedGuids is populated so Step 3 filters them out.
                        if (unchangedGuids == null && existingComponents.Count > 0)
                        {
                            unchangedGuids = existingComponents.Keys.ToHashSet();
                        }

                        // Capture external connections for replaced components before removal
                        if (componentsToReplace.Count > 0)
                        {
                            capturedConnections = GhJsonGrasshopper.CaptureExternalConnections(componentsToReplace).ToList();
                            Debug.WriteLine($"[gh_put] Captured {capturedConnections.Count} external connections");
                        }
                    }

                    // Step 3: Filter the document to only include components that need placement.
                    // Unchanged existing components are stripped out so they are not duplicated.
                    if (document.Components != null)
                    {
                        var filteredComponents = new List<GhJsonComponent>();
                        var skippedGuids = new HashSet<Guid>();

                        foreach (var comp in document.Components)
                        {
                            if (comp.InstanceGuid.HasValue && comp.InstanceGuid.Value != Guid.Empty)
                            {
                                var guid = comp.InstanceGuid.Value;
                                if (unchangedGuids != null && unchangedGuids.Contains(guid))
                                {
                                    // This is an unchanged existing component — skip it
                                    skippedGuids.Add(guid);
                                    continue;
                                }
                            }

                            filteredComponents.Add(comp);
                        }

                        if (skippedGuids.Count > 0)
                        {
                            Debug.WriteLine($"[gh_put] Filtered out {skippedGuids.Count} unchanged component(s) from document");

                            // Build a set of remaining component IDs for connection filtering
                            var remainingIds = new HashSet<int>(filteredComponents.Where(c => c.Id.HasValue).Select(c => c.Id.Value));

                            // Filter connections to only those between remaining components
                            var filteredConnections = document.Connections?.Where(conn =>
                                remainingIds.Contains(conn.From.Id) && remainingIds.Contains(conn.To.Id)).ToList();

                            // Filter groups to only those with at least one remaining member
                            var filteredGroups = document.Groups?.Where(g =>
                                g.Members?.Any(m => remainingIds.Contains(m)) == true).ToList();

                            document = new GhJsonDocument(
                                document.Schema,
                                document.Metadata,
                                filteredComponents,
                                filteredConnections,
                                filteredGroups);
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
                        var ghDoc = GhJsonGrasshopper.GetActiveDocument();

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
                            AutoOffset = autoOffset,
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
                                if (protectedGuids.Contains(conn.SourceGuid) || protectedGuids.Contains(conn.TargetGuid))
                                {
                                    continue;
                                }

                                try
                                {
                                    var success = GhJsonGrasshopper.Connect(
                                        conn.SourceGuid,
                                        conn.TargetGuid,
                                        conn.SourceParamName,
                                        conn.TargetParamName);

                                    if (success)
                                    {
                                        restored++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gh_put] Error restoring connection {conn.SourceGuid}.{conn.SourceParamName} -> {conn.TargetGuid}.{conn.TargetParamName}: {ex.Message}");
                                }
                            }

                            Debug.WriteLine($"[gh_put] Restored {restored}/{capturedConnections.Count} external connections");

                            if (restored > 0)
                            {
                                ghDoc?.NewSolution(false);
                                Instances.RedrawCanvas();
                            }
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

                // Build a combined analysis message that surfaces silent failures
                // (FailedComponents / Warnings) which CanvasPlacer collects when
                // SkipInvalidComponents is true. Without this, a Put that drops
                // every component would still report Success and produce no UI feedback.
                var analysisSections = new List<string>();
                if (!string.IsNullOrWhiteSpace(analysisMsg))
                {
                    analysisSections.Add(analysisMsg);
                }

                if (putResult.FailedComponents != null && putResult.FailedComponents.Count > 0)
                {
                    var lines = new List<string> { "Errors:" };
                    foreach (var failed in putResult.FailedComponents)
                    {
                        lines.Add($"- Could not instantiate component '{failed}'. Component is unknown to the active Grasshopper installation.");
                    }

                    analysisSections.Add(string.Join("\n", lines));
                }

                if (putResult.Warnings != null && putResult.Warnings.Count > 0)
                {
                    var lines = new List<string> { "Warnings:" };
                    foreach (var w in putResult.Warnings)
                    {
                        lines.Add($"- {w}");
                    }

                    analysisSections.Add(string.Join("\n", lines));
                }

                var combinedAnalysis = analysisSections.Count > 0
                    ? string.Join("\n", analysisSections)
                    : null;

                var toolResult = new JObject
                {
                    ["components"] = JArray.FromObject(placedNames),
                    ["instanceGuids"] = JArray.FromObject(placedGuids),
                    ["analysis"] = combinedAnalysis,
                };

                if (placedGuids.Count == 0)
                {
                    var emptyMessage = string.IsNullOrWhiteSpace(combinedAnalysis)
                        ? "No components could be placed. The GhJSON may be empty, all components may be invalid, or the document may already be up to date."
                        : combinedAnalysis;

                    output.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Tool, emptyMessage);
                }

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
        /// Extracts the GhJSON string from the tool argument. Handles cases where the model
        /// passes the JSON as a string value, an object, or an array.
        /// </summary>
        /// <param name="token">The JToken containing the ghjson argument.</param>
        /// <returns>The GhJSON string, or an empty string if the argument is missing.</returns>
        private static string ExtractGhJsonString(JToken token)
        {
            if (token == null)
            {
                return string.Empty;
            }

            if (token.Type == JTokenType.Object || token.Type == JTokenType.Array)
            {
                return token.ToString(Formatting.None);
            }

            if (token.Type == JTokenType.String)
            {
                var value = token.Value<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        var path = value.Trim();
                        if (File.Exists(path))
                        {
                            var extension = Path.GetExtension(path);
                            if (string.Equals(extension, ".ghjson", StringComparison.OrdinalIgnoreCase))
                            {
                                return File.ReadAllText(path);
                            }
                        }
                    }
                    catch
                    {
                        // Not a valid path; fall back to returning the literal value.
                    }
                }

                return value;
            }

            return token.ToString();
        }

        /// <summary>
        /// Compares two GhJSON component representations for structural equality,
        /// ignoring volatile fields such as runtime messages, IDs, and selection state.
        /// </summary>
        private static bool ComponentsAreEqual(GhJsonComponent incoming, GhJsonComponent existing)
        {
            var incomingJObj = JObject.FromObject(incoming);
            var existingJObj = JObject.FromObject(existing);

            // Remove volatile fields that should not affect equality
            incomingJObj.Remove("id");
            incomingJObj.Remove("errors");
            incomingJObj.Remove("warnings");
            incomingJObj.Remove("remarks");

            existingJObj.Remove("id");
            existingJObj.Remove("errors");
            existingJObj.Remove("warnings");
            existingJObj.Remove("remarks");

            // Remove selection state from componentState if present
            if (incomingJObj["componentState"] is JObject incomingState)
            {
                incomingState.Remove("selected");
            }

            if (existingJObj["componentState"] is JObject existingState)
            {
                existingState.Remove("selected");
            }

            // Remove runtime data from parameter settings. Runtime data is computed by the
            // canvas and is not part of the component's structural definition, so including
            // it makes every existing component look different from an incoming definition.
            static void RemoveRuntimeData(JObject jObj)
            {
                foreach (var settingsKey in new[] { "inputSettings", "outputSettings" })
                {
                    if (jObj[settingsKey] is JArray settingsArray)
                    {
                        foreach (var item in settingsArray)
                        {
                            if (item is JObject itemObj)
                            {
                                itemObj.Remove("runtimeData");
                            }
                        }
                    }
                }
            }

            RemoveRuntimeData(incomingJObj);
            RemoveRuntimeData(existingJObj);

            return JToken.DeepEquals(incomingJObj, existingJObj);
        }

    }
}