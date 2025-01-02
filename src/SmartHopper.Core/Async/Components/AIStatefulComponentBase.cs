/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/lamest/AsyncComponent
 * MIT License
 * Copyright (c) 2022 Ivan Sukhikh
 */

using System;
using System.Threading.Tasks;
using System.Threading;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Utils;
using SmartHopper.Core.Async.Workers;
using System.Diagnostics;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Windows.Forms;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Config.Providers;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Configuration;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.Async.Components
{
    /// <summary>
    /// Base class for AI-powered stateful components that need to make API calls to AI providers
    /// </summary>
    public abstract class AIStatefulComponentBase : AsyncStatefulComponentBase
    {
        protected GH_Structure<GH_String> LastMetrics { get; private set; } // Useless? Move metrics here?
        protected string ApiKey { get; set; }
        protected string Model { get; set; }
        protected string SelectedProvider { get; private set; }
        
        private const int MIN_DEBOUNCE_TIME = 1000;
        private volatile bool _isDebouncing;
        private System.Threading.Timer _debounceTimer;
        
        protected AIStatefulComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            LastMetrics = new GH_Structure<GH_String>(); // Useless? Move metrics here?
            SelectedProvider = MistralAI.ProviderName; // Default to SmartHopper
        }

        protected override sealed void RegisterInputParams(GH_InputParamManager pManager)
        {
            Debug.WriteLine("[AIStatefulComponentBase] RegisterInputParams - Start");
            // Allow derived classes to add their specific inputs
            RegisterAdditionalInputParams(pManager);
            
            // Common AI component inputs
            pManager.AddTextParameter("Model", "M", "The model to use (leave empty to use the default model)", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Run", "R", "Set to true to execute", GH_ParamAccess.item);
            Debug.WriteLine("[AIStatefulComponentBase] RegisterInputParams - Complete");
        }

        protected override sealed void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            Debug.WriteLine("[AIStatefulComponentBase] RegisterOutputParams - Start");
            // Register component-specific outputs first
            RegisterAdditionalOutputParams(pManager);
            
            // Combined metrics output as JSON
            pManager.AddTextParameter("Metrics", "M", "Usage metrics in JSON format including input tokens, output tokens, and finish reason", GH_ParamAccess.item);
            Debug.WriteLine("[AIStatefulComponentBase] RegisterOutputParams - Complete");
        }

        /// <summary>
        /// Register component-specific input parameters
        /// </summary>
        protected abstract void RegisterAdditionalInputParams(GH_InputParamManager pManager);

        /// <summary>
        /// Register the main output parameters specific to this component
        /// </summary>
        protected abstract void RegisterAdditionalOutputParams(GH_OutputParamManager pManager);

        /// <summary>
        /// Get the prompt to send to the AI service
        /// </summary>
        protected abstract string GetPrompt(IGH_DataAccess DA);

        /// <summary>
        /// Process the AI response and return true if successful
        /// </summary>
        protected abstract bool ProcessFinalResponse(AIResponse response, IGH_DataAccess DA);

        /// <summary>
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason)
        /// </summary>
        /// <param name="DA">The data access object</param>
        /// <param name="baseOutputIndex">The index of the first metrics output parameter</param>
        protected void SetMetricsOutput(IGH_DataAccess DA, AIResponse response, int initialBranches = 0, int processedBranches = 0)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");
            if (response == null)
            {
                Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - No response, skipping metrics");
                return;
            }

            // Handle potential non-numeric token values
            int inTokenValue;
            int outTokenValue;

            if (response.InTokens is int inTokenInt)
            {
                inTokenValue = inTokenInt;
            }
            else
            {
                int.TryParse(response.InTokens.ToString(), out inTokenValue);
            }

            if (response.OutTokens is int outTokenInt)
            {
                outTokenValue = outTokenInt;
            }
            else
            {
                int.TryParse(response.OutTokens.ToString(), out outTokenValue);
            }

            // Create JSON object with metrics
            var metricsJson = new JObject(
                new JProperty("tokens_input", inTokenValue),
                new JProperty("tokens_output", outTokenValue),
                new JProperty("finish_reason", response.FinishReason),
                new JProperty("completion_time", response.CompletionTime)
            );

            // If initialBranches are provided, add them to the JSON object
            if (initialBranches > 0)
            {
                metricsJson.Add("branches_input", initialBranches);
            }
            // If processedBranches are provided, add them to the JSON object
            if (processedBranches > 0)
            {
                metricsJson.Add("branches_processed", processedBranches);
            }

            //metricsStructure.Append(new GH_String(metricsJson.ToString()), path);

            // Get the number of additional outputs from the derived component
            int additionalOutputCount = 0;
            var outputParams = Params.Output;
            for (int i = 0; i < outputParams.Count; i++)
            {
                if (outputParams[i].Name == "Metrics")
                {
                    additionalOutputCount = i;
                    break;
                }
            }

            DA.SetData(additionalOutputCount, metricsJson);
            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics at index {additionalOutputCount}. JSON: {metricsJson}");
        }

        protected abstract class AIWorkerBase : StatefulWorker
        {
            protected AIResponse _lastAIResponse;
            protected string ApiKey { get; private set; }
            protected string Model { get; private set; }
            protected string Prompt { get; private set; }

            protected AIWorkerBase(
                Action<string> progressReporter, 
                AIStatefulComponentBase parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage) 
                : base(progressReporter, parent, addRuntimeMessage)
            {
            }

            public override void GatherInput(IGH_DataAccess DA, GH_ComponentParamServer p)
            {
                Debug.WriteLine("[AIWorkerBase] GatherInput - Start");
                string model = null;
                DA.GetData("Model", ref model);
                Debug.WriteLine($"[AIWorkerBase] GatherInput - Model: {model}");

                Model = model;

                var parentComponent = (AIStatefulComponentBase)_parent;
                Debug.WriteLine($"[AIWorkerBase] GatherInput - Parent component: {(parentComponent == null ? "null" : parentComponent.GetType().Name)}");

                Prompt = parentComponent.GetPrompt(DA);
            }

            /// <summary>
            /// Process a value with AI using a specific prompt. To be implemented by derived classes
            /// to handle their specific AI processing needs.
            /// </summary>
            protected abstract Task<List<IGH_Goo>> ProcessAIResponse(
                string value,
                string prompt,
                CancellationToken ct);

            protected async Task<AIResponse> GetResponse(List<KeyValuePair<string, string>> messages, CancellationToken token)
            {
                if (((AIStatefulComponentBase)_parent)._isDebouncing)
                {
                    Debug.WriteLine("[AIWorkerBase] GetResponse - Debouncing, skipping request");
                    return new AIResponse
                    {
                        Response = "Too many requests, please wait...",
                        FinishReason = "error",
                        InTokens = 0,
                        OutTokens = 0
                    };
                }

                try
                {
                    Debug.WriteLine($"[AIWorkerBase] GetResponse - Using Provider: {((AIStatefulComponentBase)_parent).SelectedProvider}");
                    var (apiKey, modelToUse) = ((AIStatefulComponentBase)_parent).GetAIConfiguration();

                    Debug.WriteLine("[AIWorkerBase] Number of messages: " + messages.Count);
                    Debug.WriteLine("[AIWorkerBase] Prompt: " + Prompt);
                    Debug.WriteLine("[AIWorkerBase] Model: " + modelToUse);

                    if (messages == null || !messages.Any())
                    {
                        return null;
                    }

                    // Get endpoint based on component type
                    string endpoint = "";

                    var prevInTokens = _lastAIResponse?.InTokens ?? 0;
                    var prevOutTokens = _lastAIResponse?.OutTokens ?? 0;

                    var response = await AIUtils.GetResponse(
                        ((AIStatefulComponentBase)_parent).SelectedProvider,
                        modelToUse,
                        messages,
                        endpoint: endpoint);

                    // if _lastAIResponse is empty or null, set it to the response
                    if (_lastAIResponse == null || string.IsNullOrEmpty(_lastAIResponse.Response))
                    {
                        _lastAIResponse = response;
                        //_lastAIResponse.InTokens += prevInTokens;
                        //_lastAIResponse.OutTokens += prevOutTokens;
                    }

                    _lastAIResponse.InTokens += response.InTokens;
                    _lastAIResponse.OutTokens += response.OutTokens;

                    return response;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIWorkerBase] GetResponse - Exception: {ex.Message}");
                    return new AIResponse
                    {
                        Response = ex.Message,
                        FinishReason = "error"
                    };
                }
            }

            protected static bool TryParseJson(string json, out JToken token)
            {
                return AIUtils.TryParseJson(json, out token);
            }
        }

        protected static int GetDebounceTime()
        {
            var settingsDebounceTime = SmartHopperSettings.Load().DebounceTime;
            return Math.Max(settingsDebounceTime, MIN_DEBOUNCE_TIME);
        }

        protected (string apiKey, string model) GetAIConfiguration()
        {
            var settings = SmartHopperSettings.Load();
            string apiKey = null;

            // Use the provider selected from menu
            string providerName = SelectedProvider;
            
            if (settings.ProviderSettings.ContainsKey(providerName) &&
                settings.ProviderSettings[providerName].ContainsKey("ApiKey"))
            {
                apiKey = settings.ProviderSettings[providerName]["ApiKey"].ToString();
            }

            // If model is empty, use default from settings
            string modelToUse = string.IsNullOrEmpty(Model) ? 
                (settings.ProviderSettings[providerName].ContainsKey("DefaultModel") ? 
                    settings.ProviderSettings[providerName]["DefaultModel"].ToString() : 
                    Model) : 
                Model;

            return (apiKey, modelToUse);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // Add provider selection submenu
            var providersMenu = new ToolStripMenuItem("Select Provider");
            menu.Items.Add(providersMenu);

            // Get all available providers
            var providers = SmartHopperSettings.DiscoverProviders();
            foreach (var provider in providers)
            {
                var item = new ToolStripMenuItem(provider.Name)
                {
                    Checked = provider.Name == SelectedProvider,
                    CheckOnClick = true,
                    Tag = provider.Name
                };

                item.Click += (s, e) =>
                {
                    var menuItem = s as ToolStripMenuItem;
                    if (menuItem != null)
                    {
                        // Uncheck all other items
                        foreach (ToolStripMenuItem otherItem in providersMenu.DropDownItems)
                        {
                            if (otherItem != menuItem)
                                otherItem.Checked = false;
                        }

                        SelectedProvider = menuItem.Tag.ToString();
                        this.ExpireSolution(true);
                    }
                };

                providersMenu.DropDownItems.Add(item);
            }
        }

        protected override void OnStateChanged(ComponentState newState)
        {
            base.OnStateChanged(newState);
            
            switch (newState)
            {
                case ComponentState.Completed:
                    Debug.WriteLine("[AIStatefulComponentBase] OnStateChanged - Starting debounce timer");
                    _isDebouncing = true;
                    _debounceTimer?.Dispose();
                    _debounceTimer = new System.Threading.Timer(_ =>
                    {
                        _isDebouncing = false;
                        Debug.WriteLine("[AIStatefulComponentBase] OnStateChanged - Debounce timer elapsed");
                    }, null, GetDebounceTime(), Timeout.Infinite);
                    break;
            }
        }
    }
}
