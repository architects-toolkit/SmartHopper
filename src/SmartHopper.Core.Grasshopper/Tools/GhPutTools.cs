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
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Core.Graph;

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
            try
            {
                var json = parameters["json"]?.ToString() ?? string.Empty;
                var document = GrasshopperJsonConverter.DeserializeFromJson(json);
                if (document?.Components == null || !document.Components.Any())
                {
                    return new { success = false, error = "JSON must contain a non-empty components array" };
                }

                var guidMapping = new Dictionary<Guid, Guid>();
                var startPoint = GHCanvasUtils.StartPoint(100);

                // Always compute positions, even if pivots are already set; CreateComponentGrid handles existing pivots
                try
                {
                    var positions = DependencyGraphUtils.CreateComponentGrid(document);
                    foreach (var component in document.Components)
                    {
                        if (positions.TryGetValue(component.InstanceGuid.ToString(), out var pos))
                            component.Pivot = new PointF(pos.X, pos.Y);
                    }
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
                            continue;
                        if (!guidMapping.TryGetValue(connection.From.ComponentId, out var src) ||
                            !guidMapping.TryGetValue(connection.To.ComponentId, out var tgt))
                            continue;

                        var srcObj = GHCanvasUtils.FindInstance(src);
                        var tgtObj = GHCanvasUtils.FindInstance(tgt);
                        if (srcObj == null || tgtObj == null) continue;

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

                var names = document.Components.Select(c => c.Name).Distinct().ToList();
                return new { success = true, components = names };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PutTools] Error in GhPutToolAsync: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
    }

    #endregion
}
