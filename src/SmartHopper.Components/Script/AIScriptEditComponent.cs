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
    public class AIScriptEditComponent : AISelectingStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("6C9B9D73-5A62-4E87-8C1C-7C9F6CDE8912");

        protected override Bitmap Icon => Resources.textevaluate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Json;

        public AIScriptEditComponent()
            : base(
                  "AI Script Edit",
                  "AIScriptEdit",
                  "Edit an existing Grasshopper script component using natural language instructions.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Guid", "G", "Optional GUID of the script component to edit. If empty, uses the first selected script component.", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Prompt", "P", "REQUIRED instructions describing how to edit the script.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Include Current Code", "C", "Include the current script code in the AI prompt for context.", GH_ParamAccess.item, true);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Script", "S", "Updated script code.", GH_ParamAccess.item);
            pManager.AddTextParameter("Guid", "G", "Instance GUID of the edited script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Sm", "Brief summary of the changes.", GH_ParamAccess.item);
            pManager.AddTextParameter("Inputs", "I", "JSON array describing the updated script inputs returned by the tool.", GH_ParamAccess.item);
            pManager.AddTextParameter("Outputs", "O", "JSON array describing the updated script outputs returned by the tool.", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "M", "Informational message from the tool.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIScriptEditWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AIScriptEditWorker : AsyncWorkerBase
        {
            private readonly AIScriptEditComponent parent;
            private string guid;
            private string prompt;
            private bool includeCurrentCode;
            private bool hasWork;
            private readonly Dictionary<string, GH_String> result = new Dictionary<string, GH_String>();

            public AIScriptEditWorker(
                AIScriptEditComponent parent,
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
                bool includeCode = true;
                DA.GetData("Include Current Code", ref includeCode);

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
                this.includeCurrentCode = includeCode;

                this.hasWork = !string.IsNullOrWhiteSpace(this.guid) && !string.IsNullOrWhiteSpace(this.prompt);
                if (!this.hasWork)
                {
                    if (string.IsNullOrWhiteSpace(this.guid))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No GUID provided and no component selected.");
                    }

                    if (string.IsNullOrWhiteSpace(this.prompt))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Prompt is required.");
                    }
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
                        ["guid"] = this.guid,
                        ["prompt"] = this.prompt,
                        ["includeCurrentCode"] = this.includeCurrentCode,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await this.parent.CallAiToolAsync("script_edit", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.result["Message"] = new GH_String("Tool 'script_edit' returned no result.");
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

                        this.result["Message"] = new GH_String("Script edit failed. See runtime errors for details.");
                        return;
                    }

                    string script = toolResult["script"]?.ToString() ?? string.Empty;
                    string guid = toolResult["guid"]?.ToString() ?? this.guid;
                    string changesSummary = toolResult["changesSummary"]?.ToString() ?? "Script updated.";
                    string message = toolResult["message"]?.ToString() ?? "Script component updated successfully.";
                    string inputsJson = toolResult["inputs"]?.ToString() ?? string.Empty;
                    string outputsJson = toolResult["outputs"]?.ToString() ?? string.Empty;

                    this.result["Script"] = new GH_String(script);
                    this.result["Guid"] = new GH_String(guid);
                    this.result["Summary"] = new GH_String(changesSummary);
                    this.result["Inputs"] = new GH_String(inputsJson);
                    this.result["Outputs"] = new GH_String(outputsJson);
                    this.result["Message"] = new GH_String(message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptEditWorker] Error: {ex.Message}");
                    this.result["Message"] = new GH_String($"Error editing script: {ex.Message}");
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
                    message = "No script edited";
                }
            }
        }
    }
}
