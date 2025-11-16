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
    public class AIScriptNewComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("3E0D9D6C-1B9D-4F77-9D50-23BBA198F201");

        protected override Bitmap Icon => Resources.textgenerate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Json;

        public AIScriptNewComponent()
            : base(
                  "AI Script New",
                  "AIScriptNew",
                  "Generate a new Grasshopper script component from natural language instructions.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED instructions describing the script to generate.", GH_ParamAccess.item);
            pManager.AddTextParameter("Language", "L", "Optional language for the script: Python (default), C#, IronPython or VB.", GH_ParamAccess.item, "python");
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Script", "S", "Generated script code.", GH_ParamAccess.item);
            pManager.AddTextParameter("Guid", "G", "Instance GUID of the created script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Language", "L", "Language used for the created script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Component Name", "C", "Name of the created script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Input Count", "IC", "Number of inputs created on the script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Count", "OC", "Number of outputs created on the script component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "M", "Informational message from the tool.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIScriptNewWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AIScriptNewWorker : AsyncWorkerBase
        {
            private readonly AIScriptNewComponent parent;
            private string prompt;
            private string language;
            private readonly Dictionary<string, GH_String> result = new Dictionary<string, GH_String>();
            private bool hasWork;

            public AIScriptNewWorker(
                AIScriptNewComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localPrompt = null;
                DA.GetData("Prompt", ref localPrompt);
                string localLanguage = "python";
                DA.GetData("Language", ref localLanguage);

                this.prompt = localPrompt ?? string.Empty;
                this.language = string.IsNullOrWhiteSpace(localLanguage) ? "python" : localLanguage;

                this.hasWork = !string.IsNullOrWhiteSpace(this.prompt);
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
                        ["prompt"] = this.prompt,
                        ["language"] = this.language,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await this.parent.CallAiToolAsync("script_new", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.result["Message"] = new GH_String("Tool 'script_new' returned no result.");
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

                        this.result["Message"] = new GH_String("Script generation failed. See runtime errors for details.");
                        return;
                    }

                    string script = toolResult["script"]?.ToString() ?? string.Empty;
                    string guid = toolResult["guid"]?.ToString() ?? string.Empty;
                    string lang = toolResult["language"]?.ToString() ?? this.language;
                    string componentName = toolResult["componentName"]?.ToString() ?? string.Empty;
                    string message = toolResult["message"]?.ToString() ?? "Script component created successfully.";
                    string inputCount = toolResult["inputCount"]?.ToString() ?? string.Empty;
                    string outputCount = toolResult["outputCount"]?.ToString() ?? string.Empty;

                    this.result["Script"] = new GH_String(script);
                    this.result["Guid"] = new GH_String(guid);
                    this.result["Language"] = new GH_String(lang);
                    this.result["ComponentName"] = new GH_String(componentName);
                    this.result["InputCount"] = new GH_String(inputCount);
                    this.result["OutputCount"] = new GH_String(outputCount);
                    this.result["Message"] = new GH_String(message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptNewWorker] Error: {ex.Message}");
                    this.result["Message"] = new GH_String($"Error generating script: {ex.Message}");
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

                if (this.result.TryGetValue("Message", out GH_String msgValue))
                {
                    this.parent.SetPersistentOutput("Message", msgValue, DA);
                    message = msgValue.Value;
                }
                else
                {
                    message = "No script generated";
                }
            }
        }
    }
}
