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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Context provider that supplies full metadata about the currently selected
    /// Grasshopper objects, including their topology and a small runtime preview.
    /// </summary>
    public class SelectionContextProvider : IAIContextProvider
    {
        /// <summary>
        /// Maximum number of selected objects to include in the detailed list.
        /// </summary>
        private const int MaxSelectedObjects = 25;

        /// <summary>
        /// Maximum number of output parameters sampled for runtime preview per component.
        /// </summary>
        private const int MaxOutputsPerComponent = 5;

        /// <summary>
        /// Maximum number of data-tree branches to sample per output parameter.
        /// </summary>
        private const int MaxBranchesPerOutput = 3;

        /// <summary>
        /// Maximum number of items to sample per branch.
        /// </summary>
        private const int MaxItemsPerBranch = 3;

        /// <summary>
        /// Maximum length of a single branch preview string.
        /// </summary>
        private const int MaxBranchPreviewLength = 50;

        /// <summary>
        /// Maximum total length of a runtime preview string.
        /// </summary>
        private const int MaxTotalPreviewLength = 200;

        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        public string ProviderId => "selection";

        /// <summary>
        /// Gets the current selection context for AI queries.
        /// </summary>
        /// <returns>A dictionary containing the current selection context values.</returns>
        [SuppressMessage("Design", "CA1031", Justification = "Context providers must be resilient and never crash the AI request pipeline.")]
        public Dictionary<string, string> GetContext()
        {
            try
            {
                int selectedCount = 0;
                int selectedComponentCount = 0;
                int selectedParamCount = 0;
                string selectedObjectsJson = "[]";
                string selectedTopologyJson = "[]";
                string selectedRuntimeValuesJson = "[]";

                using (var uiThreadComplete = new ManualResetEventSlim(false))
                {
                    RhinoApp.InvokeOnUiThread(
                        (Action)(() =>
                        {
                            try
                            {
                                var canvas = Instances.ActiveCanvas;
                                var doc = canvas?.Document;
                                if (doc == null)
                                {
                                    return;
                                }

                                var selected = doc.Objects
                                    ?.OfType<IGH_DocumentObject>()
                                    ?.Where(o => o?.Attributes?.Selected == true)
                                    ?.ToList();

                                selectedCount = selected?.Count ?? 0;
                                selectedComponentCount = selected?.OfType<IGH_Component>().Count() ?? 0;
                                selectedParamCount = selected?.OfType<IGH_Param>().Count() ?? 0;

                                if (selected != null && selected.Count > 0)
                                {
                                    var objectInfos = new List<object>();
                                    var topology = new List<object>();
                                    var runtimeValues = new List<object>();

                                    var selectedGuids = new HashSet<Guid>(selected.Select(o => o.InstanceGuid));

                                    foreach (var obj in selected.Take(MaxSelectedObjects))
                                    {
                                        objectInfos.Add(BuildObjectInfo(obj));
                                        CollectTopology(obj, selectedGuids, topology);
                                        CollectRuntimePreview(obj, runtimeValues);
                                    }

                                    selectedObjectsJson = JsonConvert.SerializeObject(objectInfos);
                                    selectedTopologyJson = JsonConvert.SerializeObject(topology);
                                    selectedRuntimeValuesJson = JsonConvert.SerializeObject(runtimeValues);
                                }
                            }
                            finally
                            {
                                uiThreadComplete.Set();
                            }
                        }));

                    uiThreadComplete.Wait(TimeSpan.FromSeconds(5));
                }

                return new Dictionary<string, string>
                {
                    { "selected-count", selectedCount.ToString(CultureInfo.InvariantCulture) },
                    { "selected-component-count", selectedComponentCount.ToString(CultureInfo.InvariantCulture) },
                    { "selected-param-count", selectedParamCount.ToString(CultureInfo.InvariantCulture) },
                    { "selected-objects", selectedObjectsJson },
                    { "selected-topology", selectedTopologyJson },
                    { "selected-runtime-values", selectedRuntimeValuesJson },
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectionContextProvider] Error: {ex.Message}");
                return GetFallback();
            }
        }

        /// <summary>
        /// Returns a fallback dictionary when the active canvas cannot be read.
        /// </summary>
        private static Dictionary<string, string> GetFallback()
        {
            return new Dictionary<string, string>
            {
                { "selected-count", "0" },
                { "selected-component-count", "0" },
                { "selected-param-count", "0" },
                { "selected-objects", "[]" },
                { "selected-topology", "[]" },
                { "selected-runtime-values", "[]" },
            };
        }

        /// <summary>
        /// Builds a compact information object for a single selected document object.
        /// </summary>
        private static object BuildObjectInfo(IGH_DocumentObject obj)
        {
            return new
            {
                instanceGuid = obj.InstanceGuid,
                name = obj.Name,
                nickName = string.IsNullOrWhiteSpace(obj.NickName) ? obj.Name : obj.NickName,
                type = obj.GetType().Name,
                pivot = new
                {
                    x = obj.Attributes?.Pivot.X ?? 0f,
                    y = obj.Attributes?.Pivot.Y ?? 0f,
                },
                selected = obj.Attributes?.Selected ?? false,
                locked = (obj as IGH_ActiveObject)?.Locked ?? false,
            };
        }

        /// <summary>
        /// Collects wire connections whose source and target both belong to the selected set.
        /// </summary>
        private static void CollectTopology(IGH_DocumentObject obj, HashSet<Guid> selectedGuids, List<object> topology)
        {
            IList<IGH_Param>? outputs = null;

            if (obj is IGH_Component component)
            {
                outputs = component.Params.Output;
            }
            else if (obj is IGH_Param param)
            {
                outputs = new[] { param };
            }

            if (outputs == null)
            {
                return;
            }

            foreach (var output in outputs)
            {
                foreach (var recipient in output.Recipients)
                {
                    var targetObj = recipient.Attributes?.GetTopLevel?.DocObject;
                    if (targetObj != null && selectedGuids.Contains(targetObj.InstanceGuid))
                    {
                        topology.Add(new
                        {
                            sourceGuid = obj.InstanceGuid,
                            sourceParam = string.IsNullOrWhiteSpace(output.NickName) ? output.Name : output.NickName,
                            targetGuid = targetObj.InstanceGuid,
                            targetParam = string.IsNullOrWhiteSpace(recipient.NickName) ? recipient.Name : recipient.NickName,
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Collects a short preview of the runtime values for a selected object.
        /// </summary>
        private static void CollectRuntimePreview(IGH_DocumentObject obj, List<object> runtimeValues)
        {
            if (obj is IGH_Component component)
            {
                var outputPreviews = new List<object>();

                foreach (var output in component.Params.Output.Take(MaxOutputsPerComponent))
                {
                    var preview = BuildOutputPreview(output);
                    if (!string.IsNullOrWhiteSpace(preview))
                    {
                        outputPreviews.Add(new { name = string.IsNullOrWhiteSpace(output.NickName) ? output.Name : output.NickName, preview });
                    }
                }

                if (outputPreviews.Count > 0)
                {
                    runtimeValues.Add(new
                    {
                        instanceGuid = component.InstanceGuid,
                        outputs = outputPreviews,
                    });
                }
            }
            else if (obj is IGH_Param param)
            {
                var preview = BuildOutputPreview(param);
                if (!string.IsNullOrWhiteSpace(preview))
                {
                    runtimeValues.Add(new
                    {
                        instanceGuid = param.InstanceGuid,
                        outputs = new[]
                        {
                            new { name = string.IsNullOrWhiteSpace(param.NickName) ? param.Name : param.NickName, preview },
                        },
                    });
                }
            }
        }

        /// <summary>
        /// Builds a short, human-readable preview of the data flowing through a parameter.
        /// </summary>
        [SuppressMessage("Design", "CA1031", Justification = "Best-effort preview; failures should return empty rather than bubble up.")]
        private static string BuildOutputPreview(IGH_Param param)
        {
            try
            {
                var data = param.VolatileData;
                if (data == null)
                {
                    return string.Empty;
                }

                var previews = new List<string>();

                foreach (var path in data.Paths.Take(MaxBranchesPerOutput))
                {
                    var branch = data.get_Branch(path);
                    if (branch == null)
                    {
                        continue;
                    }

                    var items = branch
                        .OfType<IGH_Goo>()
                        .Take(MaxItemsPerBranch)
                        .Select(g => g?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s));

                    var previewText = string.Join(", ", items);

                    if (previewText.Length > MaxBranchPreviewLength)
                    {
                        previewText = string.Concat(previewText.AsSpan(0, MaxBranchPreviewLength), "...");
                    }

                    if (!string.IsNullOrWhiteSpace(previewText))
                    {
                        previews.Add($"{path}: {previewText}");
                    }
                }

                var result = string.Join("; ", previews);

                if (result.Length > MaxTotalPreviewLength)
                {
                    result = string.Concat(result.AsSpan(0, MaxTotalPreviewLength), "...");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectionContextProvider] BuildOutputPreview error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
