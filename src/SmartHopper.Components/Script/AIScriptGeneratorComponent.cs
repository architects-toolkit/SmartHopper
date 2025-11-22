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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Script
{
    public class AIScriptGeneratorComponent : AISelectingStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("B9E66C95-2A7D-46AB-9E37-7C28F8202F4A");

        // protected override Bitmap Icon => Resources.textgenerate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Json;

        public AIScriptGeneratorComponent()
            : base(
                  "AI Script Generator",
                  "AIScriptGen",
                  "Create or edit Grasshopper script components from natural language instructions.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Guid", "G", "Optional GUID of the script component to edit. If empty and a script component is selected, the first selected one is used. If no GUID is resolved, a new script component will be created.", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Prompt", "P", "REQUIRED instructions describing how to create or edit the script.", GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Script", "S", "Script code created or updated by the AI tool.", GH_ParamAccess.item);
            pManager.AddTextParameter("Guid", "G", "Instance GUID of the affected script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Language", "L", "Language used for the script component (create mode, or empty for edits).", GH_ParamAccess.item);
            pManager.AddTextParameter("Component Name", "C", "Name of the created script component (create mode).", GH_ParamAccess.item);
            pManager.AddTextParameter("Input Count", "IC", "Number of inputs on the script component (if available).", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Count", "OC", "Number of outputs on the script component (if available).", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Sm", "Brief summary of what changed in the script (edit mode).", GH_ParamAccess.item);
            pManager.AddTextParameter("Inputs", "I", "JSON array describing the script inputs as returned by the tool.", GH_ParamAccess.item);
            pManager.AddTextParameter("Outputs", "O", "JSON array describing the script outputs as returned by the tool.", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "M", "Informational message from the tool.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIScriptGeneratorWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AIScriptGeneratorWorker : AsyncWorkerBase
        {
            private readonly AIScriptGeneratorComponent parent;
            private string guid;
            private string prompt;
            private bool hasWork;
            private readonly Dictionary<string, GH_String> result = new Dictionary<string, GH_String>();

            public AIScriptGeneratorWorker(
                AIScriptGeneratorComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string guidInput = string.Empty;
                DA.GetData("Guid", ref guidInput);

                string localPrompt = null;
                DA.GetData("Prompt", ref localPrompt);

                if (string.IsNullOrWhiteSpace(guidInput))
                {
                    var first = this.parent.SelectedObjects.FirstOrDefault();
                    this.guid = first != null ? first.InstanceGuid.ToString() : string.Empty;
                }
                else
                {
                    this.guid = guidInput;
                }

                this.prompt = localPrompt ?? string.Empty;

                this.hasWork = !string.IsNullOrWhiteSpace(this.prompt);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Prompt is required.");
                }

                dataCount = this.hasWork ? 1 : 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var parameters = new JObject
                    {
                        ["instructions"] = this.prompt,
                        ["contextFilter"] = "-*",
                    };

                    if (!string.IsNullOrWhiteSpace(this.guid))
                    {
                        parameters["guid"] = this.guid;
                    }

                    var toolResult = await this.parent.CallAiToolAsync("script_generator", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.result["Message"] = new GH_String("Tool 'script_generator' returned no result.");
                        return;
                    }

                    var hasErrors = toolResult["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                    if (hasErrors)
                    {
                        foreach (var msg in (JArray)toolResult["messages"])
                        {
                            if (msg["severity"]?.ToString() == "Error")
                            {
                                var text = msg["message"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, text);
                                }
                            }
                        }

                        this.result["Message"] = new GH_String("Script generation/editing failed. See runtime errors for details.");
                        return;
                    }

                    string script = toolResult["script"]?.ToString() ?? string.Empty;
                    string guid = toolResult["guid"]?.ToString() ?? this.guid;
                    string lang = toolResult["language"]?.ToString() ?? string.Empty;
                    string componentName = toolResult["componentName"]?.ToString() ?? string.Empty;
                    string inputCount = toolResult["inputCount"]?.ToString() ?? string.Empty;
                    string outputCount = toolResult["outputCount"]?.ToString() ?? string.Empty;
                    string changesSummary = toolResult["changesSummary"]?.ToString() ?? string.Empty;
                    string message = toolResult["message"]?.ToString() ?? "Script operation completed.";
                    string inputsJson = toolResult["inputs"]?.ToString() ?? string.Empty;
                    string outputsJson = toolResult["outputs"]?.ToString() ?? string.Empty;

                    this.result["Script"] = new GH_String(script);
                    this.result["Guid"] = new GH_String(guid);

                    if (!string.IsNullOrEmpty(lang))
                    {
                        this.result["Language"] = new GH_String(lang);
                    }

                    if (!string.IsNullOrEmpty(componentName))
                    {
                        this.result["ComponentName"] = new GH_String(componentName);
                    }

                    if (!string.IsNullOrEmpty(inputCount))
                    {
                        this.result["InputCount"] = new GH_String(inputCount);
                    }

                    if (!string.IsNullOrEmpty(outputCount))
                    {
                        this.result["OutputCount"] = new GH_String(outputCount);
                    }

                    if (!string.IsNullOrEmpty(changesSummary))
                    {
                        this.result["Summary"] = new GH_String(changesSummary);
                    }

                    if (!string.IsNullOrEmpty(inputsJson))
                    {
                        this.result["Inputs"] = new GH_String(inputsJson);
                    }

                    if (!string.IsNullOrEmpty(outputsJson))
                    {
                        this.result["Outputs"] = new GH_String(outputsJson);
                    }

                    this.result["Message"] = new GH_String(message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptGeneratorWorker] Error: {ex.Message}");
                    this.result["Message"] = new GH_String($"Error running script_generator: {ex.Message}");
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (this.result.TryGetValue("Script", out GH_String scriptValue))
                {
                    this.parent.SetPersistentOutput("Script", scriptValue, DA);
                }

                if (this.result.TryGetValue("Guid", out GH_String guidValue))
                {
                    this.parent.SetPersistentOutput("Guid", guidValue, DA);
                }

                if (this.result.TryGetValue("Language", out GH_String langValue))
                {
                    this.parent.SetPersistentOutput("Language", langValue, DA);
                }

                if (this.result.TryGetValue("ComponentName", out GH_String nameValue))
                {
                    this.parent.SetPersistentOutput("Component Name", nameValue, DA);
                }

                if (this.result.TryGetValue("InputCount", out GH_String inputCountValue))
                {
                    this.parent.SetPersistentOutput("Input Count", inputCountValue, DA);
                }

                if (this.result.TryGetValue("OutputCount", out GH_String outputCountValue))
                {
                    this.parent.SetPersistentOutput("Output Count", outputCountValue, DA);
                }

                if (this.result.TryGetValue("Summary", out GH_String summaryValue))
                {
                    this.parent.SetPersistentOutput("Summary", summaryValue, DA);
                }

                if (this.result.TryGetValue("Inputs", out GH_String inputsValue))
                {
                    this.parent.SetPersistentOutput("Inputs", inputsValue, DA);
                }

                if (this.result.TryGetValue("Outputs", out GH_String outputsValue))
                {
                    this.parent.SetPersistentOutput("Outputs", outputsValue, DA);
                }

                if (this.result.TryGetValue("Message", out GH_String msgValue))
                {
                    this.parent.SetPersistentOutput("Message", msgValue, DA);
                    message = msgValue.Value;
                }
                else
                {
                    message = "No script operation performed";
                }
            }
        }
    }
}
