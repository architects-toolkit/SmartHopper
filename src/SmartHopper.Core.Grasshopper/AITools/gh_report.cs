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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.Serialization;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.Query;
using GhJSON.Grasshopper.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides the "gh_report" AI tool for generating a comprehensive canvas status report.
    /// </summary>
    public class gh_report : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_report";

        /// <summary>
        /// System prompt for the AI summary.
        /// </summary>
        private readonly string summarySystemPrompt =
            "You are a Grasshopper definition analyst. Given a structured markdown report of a Grasshopper canvas, provide a brief summary (2-4 sentences) of: (1) the likely purpose of the file; (2) what is currently visible in the viewport. Be concise and specific.";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generate a comprehensive status report of the current Grasshopper canvas. Returns a structured markdown summary including object counts by type/topology, unique component names, group titles, scribble texts, viewport contents, file metadata, and all errors/warnings. Optionally includes an AI-generated summary of the file purpose.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""includeSummary"": {
                            ""type"": ""boolean"",
                            ""default"": false,
                            ""description"": ""When true, passes the report to an AI model to generate a brief summary of the file purpose and current view. Requires a provider/model. Default is false.""
                        }
                    }
                }",
                execute: this.GhReportToolAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput);
        }

        /// <summary>
        /// Executes the gh_report tool.
        /// </summary>
        private async Task<AIReturn> GhReportToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var includeSummary = args["includeSummary"]?.ToObject<bool>() ?? false;

                Debug.WriteLine($"[gh_report] includeSummary: {includeSummary}");

                // Gather all canvas data on UI thread
                var canvasData = GatherCanvasData();
                if (canvasData == null)
                {
                    output.CreateError("No active Grasshopper canvas found.");
                    return output;
                }

                // Build the markdown report
                var report = BuildMarkdownReport(canvasData);

                // Optionally generate AI summary
                string aiSummary = null;
                AIReturn aiResult = null;
                if (includeSummary)
                {
                    var providerName = toolCall.Provider;
                    var modelName = toolCall.Model;

                    var builder = AIBodyBuilder.Create()
                        .AddSystem(this.summarySystemPrompt)
                        .AddUser(report)
                        .WithContextFilter("-*");

                    var requestBody = builder.Build();

                    var request = new AIRequestCall();
                    request.Initialize(
                        provider: providerName,
                        model: modelName,
                        capability: AICapability.TextInput | AICapability.TextOutput,
                        endpoint: this.toolName,
                        body: requestBody);

                    aiResult = await request.Exec().ConfigureAwait(false);

                    if (aiResult.Success)
                    {
                        aiSummary = aiResult.Body?.GetLastText() ?? string.Empty;
                    }
                    else
                    {
                        aiSummary = "(AI summary generation failed)";
                    }
                }

                // Build tool result
                var toolResult = new JObject
                {
                    ["report"] = report,
                };

                if (aiSummary != null)
                {
                    toolResult["aiSummary"] = aiSummary;
                }

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(
                    toolResult,
                    toolInfo.Id,
                    toolInfo.Name,
                    aiResult?.Metrics,
                    aiResult?.Messages);

                var outImmutable = outBuilder.Build();
                output.CreateSuccess(outImmutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_report] Error: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        #region Canvas data gathering

        /// <summary>
        /// Gathers all canvas data needed for the status report.
        /// </summary>
        private static CanvasStatusData GatherCanvasData()
        {
            var canvas = Instances.ActiveCanvas;
            var doc = canvas?.Document;
            if (doc == null)
            {
                return null;
            }

            var data = new CanvasStatusData();
            var allObjects = doc.Objects.ToList();

            // File metadata
            data.FileName = !string.IsNullOrWhiteSpace(doc.FilePath)
                ? Path.GetFileName(doc.FilePath)
                : "Untitled";
            data.FilePath = doc.FilePath ?? string.Empty;

            // Viewport
            if (canvas.Viewport != null)
            {
                data.ViewportBounds = canvas.Viewport.VisibleRegion;
            }

            // Classify all objects
            var components = allObjects.OfType<IGH_Component>().ToList();
            var parameters = allObjects.OfType<IGH_Param>().ToList();
            var scribbles = allObjects.OfType<GH_Scribble>().ToList();
            var groups = allObjects.OfType<GH_Group>().ToList();
            var panels = allObjects.OfType<GH_Panel>().ToList();

            data.TotalObjectCount = allObjects.Count;
            data.ComponentCount = components.Count;
            data.ParamCount = parameters.Count;
            data.ScribbleCount = scribbles.Count;
            data.GroupCount = groups.Count;
            data.PanelCount = panels.Count;

            // Topology classification using CanvasSelector internals
            // We classify components + params (active objects that can have connections)
            var activeObjects = allObjects
                .Where(o => o is IGH_Component || o is IGH_Param)
                .ToList();

            ClassifyTopology(activeObjects, data);

            // Unique component names (components only, no params/groups/scribbles)
            data.UniqueComponentNames = components
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Groups with titles and pivots
            data.Groups = groups.Select(g => new GroupInfo
            {
                Name = string.IsNullOrWhiteSpace(g.NickName) ? "(unnamed)" : g.NickName,
                PivotX = g.Attributes?.Pivot.X ?? 0,
                PivotY = g.Attributes?.Pivot.Y ?? 0,
            }).ToList();

            // Scribbles with text and pivots
            data.Scribbles = scribbles.Select(s => new ScribbleInfo
            {
                Text = s.Text?.Trim() ?? string.Empty,
                PivotX = s.Attributes?.Pivot.X ?? 0,
                PivotY = s.Attributes?.Pivot.Y ?? 0,
            }).ToList();

            // Runtime messages (errors, warnings, remarks)
            var allActive = allObjects.OfType<IGH_ActiveObject>().ToList();
            data.ErrorCount = 0;
            data.WarningCount = 0;
            data.RemarkCount = 0;
            data.ErrorDetails = new List<RuntimeMessageInfo>();
            data.WarningDetails = new List<RuntimeMessageInfo>();

            foreach (var obj in allActive)
            {
                var errors = obj.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                var warnings = obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                var remarks = obj.RuntimeMessages(GH_RuntimeMessageLevel.Remark);

                if (errors != null && errors.Count > 0)
                {
                    data.ErrorCount += errors.Count;
                    foreach (var msg in errors)
                    {
                        data.ErrorDetails.Add(new RuntimeMessageInfo
                        {
                            ComponentName = string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName,
                            InstanceGuid = obj.InstanceGuid,
                            Message = msg,
                            PivotX = (obj as IGH_DocumentObject)?.Attributes?.Pivot.X ?? 0,
                            PivotY = (obj as IGH_DocumentObject)?.Attributes?.Pivot.Y ?? 0,
                        });
                    }
                }

                if (warnings != null && warnings.Count > 0)
                {
                    data.WarningCount += warnings.Count;
                    foreach (var msg in warnings)
                    {
                        data.WarningDetails.Add(new RuntimeMessageInfo
                        {
                            ComponentName = string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName,
                            InstanceGuid = obj.InstanceGuid,
                            Message = msg,
                            PivotX = (obj as IGH_DocumentObject)?.Attributes?.Pivot.X ?? 0,
                            PivotY = (obj as IGH_DocumentObject)?.Attributes?.Pivot.Y ?? 0,
                        });
                    }
                }

                if (remarks != null)
                {
                    data.RemarkCount += remarks.Count;
                }
            }

            // In-view GhJSON extract
            if (data.ViewportBounds.HasValue)
            {
                try
                {
                    var inViewObjects = CanvasSelector.FromActiveCanvas()
                        .WithViewport(data.ViewportBounds.Value)
                        .Execute();

                    var serOptions = new SerializationOptions
                    {
                        IncludeConnections = true,
                        IncludeGroups = true,
                        IncludeInternalizedData = false,
                        IncludeRuntimeMessages = false,
                        IncludeSelectedState = false,
                        AssignSequentialIds = true,
                        IncludeMetadata = false,
                    };

                    var inViewDoc = GhJsonGrasshopper.Serialize(inViewObjects, serOptions);
                    data.InViewGhJson = GhJson.ToJson(inViewDoc, new WriteOptions { Indented = false });
                    data.InViewComponentCount = inViewObjects.Count;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[gh_report] Error serializing in-view components: {ex.Message}");
                    data.InViewGhJson = null;
                }
            }

            // File metadata via GhJSON serialization
            try
            {
                var metaObjects = allObjects.Take(1).ToList();
                if (metaObjects.Count > 0)
                {
                    var metaOptions = new SerializationOptions
                    {
                        IncludeConnections = false,
                        IncludeGroups = false,
                        IncludeInternalizedData = false,
                        IncludeRuntimeMessages = false,
                        IncludeSelectedState = false,
                        AssignSequentialIds = false,
                        IncludeMetadata = true,
                    };

                    var metaDoc = GhJsonGrasshopper.Serialize(metaObjects, metaOptions);
                    if (metaDoc.Metadata != null)
                    {
                        data.MetadataJson = GhJson.ToJson(metaDoc, new WriteOptions { Indented = false });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_report] Error extracting metadata: {ex.Message}");
            }

            return data;
        }

        /// <summary>
        /// Classifies objects into topology categories by inspecting their connections.
        /// </summary>
        private static void ClassifyTopology(List<IGH_DocumentObject> activeObjects, CanvasStatusData data)
        {
            var objectGuids = new HashSet<Guid>(activeObjects.Select(o => o.InstanceGuid));

            foreach (var obj in activeObjects)
            {
                bool hasIncoming = false;
                bool hasOutgoing = false;

                // Check incoming connections
                IList<IGH_Param> inputs = null;
                if (obj is IGH_Component comp)
                {
                    inputs = comp.Params.Input;
                }
                else if (obj is IGH_Param param)
                {
                    inputs = new[] { param };
                }

                if (inputs != null)
                {
                    foreach (var input in inputs)
                    {
                        if (input.Sources != null && input.Sources.Any(s =>
                        {
                            var owner = s.Attributes?.GetTopLevel?.DocObject;
                            return owner != null && objectGuids.Contains(owner.InstanceGuid);
                        }))
                        {
                            hasIncoming = true;
                            break;
                        }
                    }
                }

                // Check outgoing connections
                IList<IGH_Param> outputs = null;
                if (obj is IGH_Component comp2)
                {
                    outputs = comp2.Params.Output;
                }
                else if (obj is IGH_Param param2)
                {
                    outputs = new[] { param2 };
                }

                if (outputs != null)
                {
                    foreach (var output in outputs)
                    {
                        if (output.Recipients != null && output.Recipients.Any(r =>
                        {
                            var owner = r.Attributes?.GetTopLevel?.DocObject;
                            return owner != null && objectGuids.Contains(owner.InstanceGuid);
                        }))
                        {
                            hasOutgoing = true;
                            break;
                        }
                    }
                }

                if (!hasIncoming && hasOutgoing)
                {
                    data.StartNodeCount++;
                }
                else if (hasIncoming && !hasOutgoing)
                {
                    data.EndNodeCount++;
                }
                else if (hasIncoming && hasOutgoing)
                {
                    data.MiddleNodeCount++;
                }
                else
                {
                    data.IsolatedNodeCount++;
                }
            }
        }

        #endregion

        #region Markdown report building

        /// <summary>
        /// Builds a structured markdown report from the gathered canvas data.
        /// </summary>
        private static string BuildMarkdownReport(CanvasStatusData data)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("# Grasshopper Canvas Status Report");
            sb.AppendLine();

            // 1. File metadata
            sb.AppendLine("## File Information");
            sb.AppendLine($"- **File name**: {data.FileName}");
            if (!string.IsNullOrWhiteSpace(data.MetadataJson))
            {
                sb.AppendLine($"- **Metadata**: `{data.MetadataJson}`");
            }

            sb.AppendLine();

            // 2. Object counts by type
            sb.AppendLine("## Object Counts");
            sb.AppendLine($"- **Total objects**: {data.TotalObjectCount}");
            sb.AppendLine($"- **Components**: {data.ComponentCount}");
            sb.AppendLine($"- **Parameters**: {data.ParamCount}");
            sb.AppendLine($"- **Panels**: {data.PanelCount}");
            sb.AppendLine($"- **Groups**: {data.GroupCount}");
            sb.AppendLine($"- **Scribbles**: {data.ScribbleCount}");
            sb.AppendLine();

            // 3. Topology counts
            sb.AppendLine("## Topology");
            sb.AppendLine($"- **Start nodes** (input/sources): {data.StartNodeCount}");
            sb.AppendLine($"- **End nodes** (output/sinks): {data.EndNodeCount}");
            sb.AppendLine($"- **Middle nodes** (processing): {data.MiddleNodeCount}");
            sb.AppendLine($"- **Isolated nodes** (no connections): {data.IsolatedNodeCount}");
            sb.AppendLine();

            // 4. Unique component names
            sb.AppendLine("## Unique Component Names");
            if (data.UniqueComponentNames.Count > 0)
            {
                sb.AppendLine(string.Join(", ", data.UniqueComponentNames));
            }
            else
            {
                sb.AppendLine("(none)");
            }

            sb.AppendLine();

            // 5. Groups
            sb.AppendLine("## Groups");
            if (data.Groups.Count > 0)
            {
                foreach (var g in data.Groups)
                {
                    sb.AppendLine($"- **{g.Name}** at ({Fmt(g.PivotX)}, {Fmt(g.PivotY)})");
                }
            }
            else
            {
                sb.AppendLine("(none)");
            }

            sb.AppendLine();

            // 6. Scribbles
            sb.AppendLine("## Scribbles");
            if (data.Scribbles.Count > 0)
            {
                foreach (var s in data.Scribbles)
                {
                    var truncated = s.Text.Length > 100 ? s.Text.Substring(0, 100) + "..." : s.Text;
                    sb.AppendLine($"- \"{truncated}\" at ({Fmt(s.PivotX)}, {Fmt(s.PivotY)})");
                }
            }
            else
            {
                sb.AppendLine("(none)");
            }

            sb.AppendLine();

            // 7. Current viewport
            sb.AppendLine("## Current Viewport");
            if (data.ViewportBounds.HasValue)
            {
                var vp = data.ViewportBounds.Value;
                sb.AppendLine($"- **Extent**: X=[{Fmt(vp.Left)}, {Fmt(vp.Right)}], Y=[{Fmt(vp.Top)}, {Fmt(vp.Bottom)}]");
                sb.AppendLine($"- **Size**: {Fmt(vp.Width)} x {Fmt(vp.Height)}");
                sb.AppendLine($"- **Components in view**: {data.InViewComponentCount}");

                if (!string.IsNullOrWhiteSpace(data.InViewGhJson))
                {
                    sb.AppendLine();
                    sb.AppendLine("### In-View GhJSON Extract");
                    sb.AppendLine($"```json");
                    sb.AppendLine(data.InViewGhJson);
                    sb.AppendLine($"```");
                }
            }
            else
            {
                sb.AppendLine("(viewport not available)");
            }

            sb.AppendLine();

            // 8. Runtime messages
            sb.AppendLine("## Runtime Messages");
            sb.AppendLine($"- **Errors**: {data.ErrorCount}");
            sb.AppendLine($"- **Warnings**: {data.WarningCount}");
            sb.AppendLine($"- **Remarks**: {data.RemarkCount}");
            sb.AppendLine();

            if (data.ErrorDetails.Count > 0)
            {
                sb.AppendLine("### Errors");
                foreach (var e in data.ErrorDetails)
                {
                    sb.AppendLine($"- **{e.ComponentName}** ({e.InstanceGuid}) at ({Fmt(e.PivotX)}, {Fmt(e.PivotY)}): {e.Message}");
                }

                sb.AppendLine();
            }

            if (data.WarningDetails.Count > 0)
            {
                sb.AppendLine("### Warnings");
                foreach (var w in data.WarningDetails)
                {
                    sb.AppendLine($"- **{w.ComponentName}** ({w.InstanceGuid}) at ({Fmt(w.PivotX)}, {Fmt(w.PivotY)}): {w.Message}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a float for display with invariant culture and no trailing zeros.
        /// </summary>
        private static string Fmt(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        #endregion

        #region Data models

        /// <summary>
        /// Holds all gathered canvas data for the status report.
        /// </summary>
        private sealed class CanvasStatusData
        {
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public string MetadataJson { get; set; }
            public RectangleF? ViewportBounds { get; set; }

            public int TotalObjectCount { get; set; }
            public int ComponentCount { get; set; }
            public int ParamCount { get; set; }
            public int ScribbleCount { get; set; }
            public int GroupCount { get; set; }
            public int PanelCount { get; set; }

            public int StartNodeCount { get; set; }
            public int EndNodeCount { get; set; }
            public int MiddleNodeCount { get; set; }
            public int IsolatedNodeCount { get; set; }

            public List<string> UniqueComponentNames { get; set; } = new List<string>();
            public List<GroupInfo> Groups { get; set; } = new List<GroupInfo>();
            public List<ScribbleInfo> Scribbles { get; set; } = new List<ScribbleInfo>();

            public int ErrorCount { get; set; }
            public int WarningCount { get; set; }
            public int RemarkCount { get; set; }
            public List<RuntimeMessageInfo> ErrorDetails { get; set; } = new List<RuntimeMessageInfo>();
            public List<RuntimeMessageInfo> WarningDetails { get; set; } = new List<RuntimeMessageInfo>();

            public string InViewGhJson { get; set; }
            public int InViewComponentCount { get; set; }
        }

        /// <summary>
        /// Information about a group on the canvas.
        /// </summary>
        private sealed class GroupInfo
        {
            public string Name { get; set; }
            public float PivotX { get; set; }
            public float PivotY { get; set; }
        }

        /// <summary>
        /// Information about a scribble on the canvas.
        /// </summary>
        private sealed class ScribbleInfo
        {
            public string Text { get; set; }
            public float PivotX { get; set; }
            public float PivotY { get; set; }
        }

        /// <summary>
        /// Information about a runtime message (error/warning).
        /// </summary>
        private sealed class RuntimeMessageInfo
        {
            public string ComponentName { get; set; }
            public Guid InstanceGuid { get; set; }
            public string Message { get; set; }
            public float PivotX { get; set; }
            public float PivotY { get; set; }
        }

        #endregion
    }
}
