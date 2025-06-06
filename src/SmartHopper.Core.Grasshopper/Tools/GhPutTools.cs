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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Tool provider for placing Grasshopper components from GhJSON format.
    /// </summary>
    public class PutTools : IAIToolProvider
    {
        #region ToolRegistration

        /// <summary>
        /// Returns the GH put tool.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_put",
                description: "Place Grasshopper components on the canvas from GhJSON format",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""json"": { ""type"": ""string"", ""description"": ""GhJSON document string"" }
                    },
                    ""required"": [""json""]
                }",
                execute: this.GhPutToolAsync
            );
        }

        #endregion

        #region GhPut

        private async Task<object> GhPutToolAsync(JObject parameters)
        {
            string analysisMsg = null;
            try
            {
                var json = parameters["json"]?.ToString() ?? string.Empty;
                GHJsonAnalyzer.Analyze(json, out analysisMsg);
                var document = GHJsonConverter.DeserializeFromJson(json);
                if (document?.Components == null || !document.Components.Any())
                {
                    var msg = analysisMsg ?? "JSON must contain a non-empty components array";
                    return new { success = false, analysis = msg };
                }

                var guidMapping = new Dictionary<Guid, Guid>();
                var startPoint = GHCanvasUtils.StartPoint(100);

                // Always compute positions, even if pivots are already set; CreateComponentGrid handles existing pivots
                try
                {
                    var nodes = DependencyGraphUtils.CreateComponentGrid(document);
                    var posMap = nodes.ToDictionary(n => n.ComponentId, n => n.Pivot);
                    foreach (var component in document.Components)
                        if (posMap.TryGetValue(component.InstanceGuid, out var pivot))
                            component.Pivot = pivot;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error generating component positions: {ex.Message}");
                }

                foreach (var component in document.Components)
                {
                    var proxy = GHObjectFactory.FindProxy(component.ComponentGuid, component.Name);
                    var instance = GHObjectFactory.CreateInstance(proxy);

                    if (instance is GH_NumberSlider slider && component.Properties != null)
                    {
                        try
                        {
                            var prop = component.Properties.GetValueOrDefault("CurrentValue");
                            if (prop?.Value != null)
                            {
                                var val = ((JObject)prop.Value)["value"]?.ToString();
                                slider.SetInitCode(val);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error setting slider value: {ex.Message}");
                        }
                    }
                    else if (component.Properties != null)
                    {
                        var filtered = component.Properties
                            .Where(kvp => !GHPropertyManager.IsPropertyOmitted(kvp.Key))
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        GHPropertyManager.SetProperties(instance, filtered);
                    }

                    var position = component.Pivot.IsEmpty
                        ? startPoint
                        : new PointF(component.Pivot.X + startPoint.X, component.Pivot.Y + startPoint.Y);

                    GHCanvasUtils.AddObjectToCanvas(instance, position, true);
                    guidMapping[component.InstanceGuid] = instance.InstanceGuid;
                }

                foreach (var connection in document.Connections)
                {
                    try
                    {
                        if (!connection.IsValid())
                        {
                            continue;
                        }

                        if (!guidMapping.TryGetValue(connection.From.ComponentId, out var src) ||
                            !guidMapping.TryGetValue(connection.To.ComponentId, out var tgt))
                        {
                            continue;
                        }

                        var srcObj = GHCanvasUtils.FindInstance(src);
                        var tgtObj = GHCanvasUtils.FindInstance(tgt);
                        if (srcObj == null || tgtObj == null)
                        {
                            continue;
                        }

                        IGH_Param? srcParam = null;
                        if (srcObj is IGH_Component sc)
                        {
                            srcParam = GHParameterUtils.GetOutputByName(sc, connection.From.ParamName);
                        }
                        else if (srcObj is IGH_Param sp)
                        {
                            srcParam = sp;
                        }

                        if (tgtObj is IGH_Component tc)
                        {
                            var tp = GHParameterUtils.GetInputByName(tc, connection.To.ParamName);
                            if (tp != null && srcParam != null)
                            {
                                GHParameterUtils.SetSource(tp, srcParam);
                            }
                        }
                        else if (tgtObj is IGH_Param tp2 && srcParam != null)
                        {
                            GHParameterUtils.SetSource(tp2, srcParam);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating connection: {ex.Message}");
                    }
                }

                var names = document.Components.Select(c => c.Name).Distinct().ToList();
                return new { success = true, components = names, analysis = analysisMsg };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PutTools] Error in GhPutToolAsync: {ex.Message}");
                var combined = string.IsNullOrEmpty(analysisMsg) ? ex.Message : analysisMsg + "\nException: " + ex.Message;
                return new { success = false, analysis = combined };
            }
        }
    }

    #endregion
}
