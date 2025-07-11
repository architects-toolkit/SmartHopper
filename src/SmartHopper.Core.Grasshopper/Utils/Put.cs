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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utility to place deserialized Grasshopper objects onto the canvas.
    /// </summary>
    internal static class Put
    {
        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, int span = 100)
        {
            var startPoint = GHCanvasUtils.StartPoint(span);
            return PutObjectsOnCanvas(document, startPoint);
        }

        /// <summary>
        /// Core placement logic; returns mapping from template InstanceGuid to placed component InstanceGuid.
        /// </summary>
        private static Dictionary<Guid, Guid> InternalPutObjects(GrasshopperDocument document, PointF startPoint)
        {
            var guidMapping = new Dictionary<Guid, Guid>();

            // Compute positions
            try
            {
                var nodes = DependencyGraphUtils.CreateComponentGrid(document);
                var posMap = nodes.ToDictionary(n => n.ComponentId, n => n.Pivot);
                foreach (var component in document.Components)
                {
                    if (posMap.TryGetValue(component.InstanceGuid, out var pivot))
                    {
                        component.Pivot = pivot;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating component positions: {ex.Message}");
            }

            // Instantiate components and set up
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
                            var initCode = prop.Value.ToString();
                            slider.SetInitCode(initCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error setting slider value: {ex.Message}");
                    }
                }
                else if (instance is IScriptComponent scriptComp && component.Properties != null)
                {
                    // clear default script inputs and outputs
                    var ghComp = (IGH_Component)scriptComp;
                    // remove all existing inputs
                    foreach (var p in ghComp.Params.Input.ToArray())
                        ghComp.Params.UnregisterInputParameter(p);
                    // remove all existing outputs
                    foreach (var p in ghComp.Params.Output.ToArray())
                        ghComp.Params.UnregisterOutputParameter(p);

                    // Script code
                    if (component.Properties.TryGetValue("Script", out var scriptProperty) && scriptProperty?.Value != null)
                    {
                        var scriptCode = scriptProperty.Value.ToString();
                        scriptComp.Text = scriptCode;
                        Debug.WriteLine($"Set script code for component {instance.InstanceGuid}, length: {scriptCode.Length}");
                    }

                    // Inputs
                    if (component.Properties.TryGetValue("ScriptInputs", out var inputsProperty) && inputsProperty?.Value is JArray inputArray)
                    {
                        foreach (JObject o in inputArray)
                        {
                            var variableName = o["variableName"]?.ToString() ?? string.Empty;
                            var prettyName = o["name"]?.ToString() ?? variableName;
                            var description = o["description"]?.ToString() ?? string.Empty;
                            var access = Enum.TryParse<GH_ParamAccess>(o["access"]?.ToString(), true, out var pa) ? pa : GH_ParamAccess.item;

                            if (GHParameterUtils.GetInputByName((IGH_Component)scriptComp, variableName) == null)
                            {
                                var param = new ScriptVariableParam(variableName)
                                {
                                    PrettyName = prettyName,
                                    Description = description,
                                    Access = access
                                };
                                param.CreateAttributes();
                                if (o["simplify"] != null)
                                    param.Simplify = o["simplify"].Value<bool>();
                                if (o["reverse"] != null)
                                    param.Reverse = o["reverse"].Value<bool>();
                                if (o["dataMapping"] != null)
                                    param.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                ((IGH_Component)scriptComp).Params.RegisterInputParam(param);
                            }
                            else
                            {
                                var existing = GHParameterUtils.GetInputByName((IGH_Component)scriptComp, variableName);
                                if (existing != null)
                                {
                                    if (o["simplify"] != null)
                                        existing.Simplify = o["simplify"].Value<bool>();
                                    if (o["reverse"] != null)
                                        existing.Reverse = o["reverse"].Value<bool>();
                                    if (o["dataMapping"] != null)
                                        existing.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                }
                            }
                        }
                    }

                    // Outputs
                    if (component.Properties.TryGetValue("ScriptOutputs", out var outputsProperty) && outputsProperty?.Value is JArray outputArray)
                    {
                        foreach (JObject o in outputArray)
                        {
                            var variableName = o["variableName"]?.ToString() ?? string.Empty;
                            var prettyName = o["name"]?.ToString() ?? variableName;
                            var description = o["description"]?.ToString() ?? string.Empty;
                            var access = Enum.TryParse<GH_ParamAccess>(o["access"]?.ToString(), true, out var pa2) ? pa2 : GH_ParamAccess.item;

                            if (GHParameterUtils.GetOutputByName((IGH_Component)scriptComp, variableName) == null)
                            {
                                var param = new ScriptVariableParam(variableName)
                                {
                                    PrettyName = prettyName,
                                    Description = description,
                                    Access = access
                                };
                                param.CreateAttributes();
                                if (o["simplify"] != null)
                                    param.Simplify = o["simplify"].Value<bool>();
                                if (o["reverse"] != null)
                                    param.Reverse = o["reverse"].Value<bool>();
                                if (o["dataMapping"] != null)
                                    param.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                ((IGH_Component)scriptComp).Params.RegisterOutputParam(param);
                            }
                            else
                            {
                                var existing = GHParameterUtils.GetOutputByName((IGH_Component)scriptComp, variableName);
                                if (existing != null)
                                {
                                    if (o["simplify"] != null)
                                        existing.Simplify = o["simplify"].Value<bool>();
                                    if (o["reverse"] != null)
                                        existing.Reverse = o["reverse"].Value<bool>();
                                    if (o["dataMapping"] != null)
                                        existing.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                }
                            }
                        }
                    }

                    // Rebuild variable parameter UI
                    ((dynamic)scriptComp).VariableParameterMaintenance();
                }
                else if (component.Properties != null)
                {
                    var filtered = component.Properties
                        .Where(kvp => !GHPropertyManager.IsPropertyOmitted(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    GHPropertyManager.SetProperties(instance, filtered);
                }

                // Position and add
                var position = component.Pivot.IsEmpty
                    ? startPoint
                    : new PointF(component.Pivot.X + startPoint.X, component.Pivot.Y + startPoint.Y);
                GHCanvasUtils.AddObjectToCanvas(instance, position, true);
                guidMapping[component.InstanceGuid] = instance.InstanceGuid;
            }

            // Connections
            foreach (var connection in document.Connections)
            {
                try
                {
                    if (!connection.IsValid())
                        continue;

                    if (!guidMapping.TryGetValue(connection.From.ComponentId, out var src) ||
                        !guidMapping.TryGetValue(connection.To.ComponentId, out var tgt))
                        continue;

                    var srcObj = GHCanvasUtils.FindInstance(src);
                    var tgtObj = GHCanvasUtils.FindInstance(tgt);
                    if (srcObj == null || tgtObj == null)
                        continue;

                    IGH_Param srcParam = null;
                    if (srcObj is IGH_Component sc)
                        srcParam = GHParameterUtils.GetOutputByName(sc, connection.From.ParamName);
                    else if (srcObj is IGH_Param sp)
                        srcParam = sp;

                    if (tgtObj is IGH_Component tc)
                    {
                        var tp = GHParameterUtils.GetInputByName(tc, connection.To.ParamName);
                        if (tp != null && srcParam != null)
                            GHParameterUtils.SetSource(tp, srcParam);
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

            return guidMapping;
        }

        /// <summary>
        /// Places components and returns their names.
        /// </summary>
        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, PointF startPoint)
        {
            var mapping = InternalPutObjects(document, startPoint);
            return document.Components.Select(c => c.Name).Distinct().ToList();
        }

        /// <summary>
        /// Places components and returns mapping; overload for span.
        /// </summary>
        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, int span = 100)
        {
            var startPoint = GHCanvasUtils.StartPoint(span);
            return InternalPutObjects(document, startPoint);
        }

        /// <summary>
        /// Places components and returns mapping; overload for point.
        /// </summary>
        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, PointF startPoint)
        {
            return InternalPutObjects(document, startPoint);
        }
    }
}
