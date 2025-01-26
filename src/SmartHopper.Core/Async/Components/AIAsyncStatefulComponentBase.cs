/* DEPRECATED */

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

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Utils;
using SmartHopper.Core.Async.Workers;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Config.Models;
using SmartHopper.Config.Providers;
using SmartHopper.Config.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using Newtonsoft.Json.Linq;


namespace SmartHopper.Core.Async.Components
{
    /// <summary>
    /// Base class for AI-powered stateful components that need to make API calls to AI providers.
    /// </summary>
    public abstract class AIStatefulComponentBase : AsyncStatefulComponentBase
    {
        /// <summary>
        /// The model to use for AI processing. Set up from the component's inputs in the GatherInput method.
        /// </summary>
        protected string Model { get; private set; }

        /// <summary>
        /// The selected AI provider. Set up from the component's dropdown menu.
        /// </summary>
        protected string SelectedProvider { get; private set; }
        
        /// <summary>
        /// Minimum debounce time in milliseconds.
        /// </summary>
        private const int MIN_DEBOUNCE_TIME = 1000;

        /// <summary>
        /// Flag indicating whether the component is currently debouncing.
        /// </summary>
        private volatile bool _isDebouncing;

        /// <summary>
        /// Debounce timer.
        /// </summary>
        private System.Threading.Timer _debounceTimer;

        /// <summary>
        /// List of AI response metrics.
        /// </summary>
        private List<AIResponse> _responseMetrics = new List<AIResponse>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AIStatefulComponentBase"/> class.
        /// </summary>
        /// <param name="name">The name of the component.</param>
        /// <param name="nickname">The nickname of the component.</param>
        /// <param name="description">The description of the component.</param>
        /// <param name="category">The category of the component.</param>
        /// <param name="subCategory">The subcategory of the component.</param>
        protected AIStatefulComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            SelectedProvider = MistralAI._name; // Default to MistralAI
        }

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override sealed void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Allow derived classes to add their specific inputs
            RegisterAdditionalInputParams(pManager);
            
            // Common AI component inputs
            pManager.AddTextParameter("Model", "M", "The model to use (leave empty to use the default model)", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Run", "R", "Set to true to execute", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers output parameters for the component.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected override sealed void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Register component-specific outputs first
            RegisterAdditionalOutputParams(pManager);
            
            // Combined metrics output as JSON
            pManager.AddTextParameter("Metrics", "M", "Usage metrics in JSON format including input tokens, output tokens, and finish reason", GH_ParamAccess.item);
        }

        /// <summary>
        /// Register component-specific input parameters, to define in derived classes.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected abstract void RegisterAdditionalInputParams(GH_InputParamManager pManager);

        /// <summary>
        /// Register component-specific output parameters, to define in derived classes.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected abstract void RegisterAdditionalOutputParams(GH_OutputParamManager pManager);

        /// <summary>
        /// Process the AI response and return true if successful.
        /// </summary>
        /// <param name="response">The AI response to process.</param>
        /// <param name="DA">The data access object.</param>
        /// <returns>True if the response was processed successfully, false otherwise.</returns>
        protected abstract bool ProcessFinalResponse(IGH_DataAccess DA);

        /// <summary>
        /// Stores the given AI response metrics in the component's internal metrics list.
        /// </summary>
        /// <param name="response">The AI response to store metrics from.</param>
        public void StoreResponseMetrics(AIResponse response)
        {
            if (response != null)
            {
                _responseMetrics.Add(response);
            }
        }

        /// <summary>
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason)
        /// </summary>
        /// <param name="DA">The data access object</param>
        /// <param name="initialBranches">The number of branches in the input data structure</param>
        protected void SetMetricsOutput(IGH_DataAccess DA, int initialBranches = 0)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            if (!_responseMetrics.Any())
            {
                Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - No response, skipping metrics");
                return;
            }

            // Aggregate metrics
            int totalInTokens = _responseMetrics.Sum(r => r.InTokens);
            int totalOutTokens = _responseMetrics.Sum(r => r.OutTokens);
            string finishReason = _responseMetrics.Last().FinishReason;
            double totalCompletionTime = _responseMetrics.Sum(r => r.CompletionTime);

            // Create JSON object with metrics
            var metricsJson = new JObject(
                new JProperty("ai_provider", _responseMetrics.Last().Provider),
                new JProperty("ai_model", _responseMetrics.Last().Model),
                new JProperty("tokens_input", totalInTokens),
                new JProperty("tokens_output", totalOutTokens),
                new JProperty("finish_reason", finishReason),
                new JProperty("completion_time", totalCompletionTime),
                new JProperty("branches_input", initialBranches),
                new JProperty("branches_processed", _responseMetrics.Count)
            );

            DA.SetData("Metrics", metricsJson);
            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsJson}");

            // Clear the stored metrics after setting the output
            _responseMetrics.Clear();
        }

        /// <summary>
        /// Sets the model to the Model property.
        /// </summary>
        /// <param name="model">The model to use.</param>
        protected void SetModel(string model)
        {
            Debug.WriteLine($"[AIStatefulComponentBase] SetModel - Setting model to: {model}");
            Model = model;
        }
        
        /// <summary>
        /// Gets the model to use from the Model property.
        /// </summary>
        /// <returns>The model to use.</returns>
        protected string GetModel()
        {
            return Model ?? ""; // "" means that the provider will use the default model
        }

        /// <summary>
        /// Gets the API's endpoint to use when getting AI responses.
        /// </summary>
        /// <returns>The API's endpoint.</returns>
        protected virtual string GetEndpoint()
        {
            return ""; // "" means that the provider will use the default endpoint
        }

        /// <summary>
        /// Gets the debounce time from the SmartHopperSettings and returns the maximum between the settings value and the minimum value defined in MIN_DEBOUNCE_TIME.
        /// </summary>
        /// <returns>The debounce time in milliseconds.</returns>
        protected static int GetDebounceTime()
        {
            var settingsDebounceTime = SmartHopperSettings.Load().DebounceTime;
            return Math.Max(settingsDebounceTime, MIN_DEBOUNCE_TIME);
        }

        /// <summary>
        /// Appends additional menu items to the component's context menu. Overrides the base method from <see cref="AsyncStatefulComponentBase"/>.
        /// </summary>
        /// <param name="menu">The menu to append to.</param>
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
                        ExpireSolution(true);
                    }
                };

                providersMenu.DropDownItems.Add(item);
            }
        }

        /// <summary>
        /// Called when the state of the component changes. Overrides the base method from <see cref="AsyncStatefulComponentBase"/>.
        /// </summary>
        /// <param name="newState"></param>
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

        /// <summary>
        /// Base class for AI workers that need to make API calls to AI providers. Inherits from StatefulWorker in the component base class <see cref="AsyncStatefulComponentBase"/>.
        /// </summary>   
        /// <param name="progressReporter">Action to report progress</param>
        /// <param name="parent">Parent component</param>
        /// <param name="addRuntimeMessage">Action to add runtime messages</param>
        protected abstract class AIWorkerBase : StatefulWorker
        {
            /// <summary>
            /// The parent stateful component.
            /// </summary>
            protected readonly AIStatefulComponentBase _parentStatefulComponent;

            /// <summary>
            /// Initializes a new instance of the <see cref="AIWorkerBase"/> class.
            /// </summary>
            /// <param name="progressReporter">Action to report progress</param>
            /// <param name="parent">Parent component</param>
            /// <param name="addRuntimeMessage">Action to add runtime messages</param>
            protected AIWorkerBase(
                Action<string> progressReporter, 
                AIStatefulComponentBase parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage) 
                : base(progressReporter, parent, addRuntimeMessage)
            {
                _parentStatefulComponent = parent;
            }

            /// <summary>
            /// Gathers input data from the data access object (DA) and sets the model and prompt for the AI worker.
            /// </summary>
            /// <param name="DA">The data access object</param>
            public override void GatherInput(IGH_DataAccess DA)
            {
                string model = null;
                DA.GetData("Model", ref model);
                Debug.WriteLine($"[AIWorkerBase] GatherInput - Model: {model}");

                Debug.WriteLine($"[AIWorkerBase] GatherInput - Parent component: {_parentStatefulComponent.GetType().Name}");

                _parentStatefulComponent.SetModel(model);
            }

            /// <summary>
            /// Process a value with AI using a specific prompt. To be implemented by derived classes.
            /// </summary>
            /// <param name="value">The value to process.</param>
            /// <param name="prompt">The prompt to use.</param>
            /// <param name="ct">The cancellation token to cancel the operation.</param>
            /// <returns>A task that represents the asynchronous operation.</returns>
            protected abstract Task<List<IGH_Goo>> ProcessAIResponse(
                string value,
                string prompt,
                CancellationToken ct);

            /// <summary>
            /// Gets a response from the AI provider using the provided messages and cancellation token.
            /// </summary>
            /// <param name="messages">The messages to send to the AI provider.</param>
            /// <param name="token">The cancellation token to cancel the operation.</param>
            /// <returns>The AI response from the provider.</returns>
            protected async Task<AIResponse> GetResponse(List<KeyValuePair<string, string>> messages, CancellationToken token)
            {
                if (_parentStatefulComponent._isDebouncing)
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
                    Debug.WriteLine($"[AIWorkerBase] GetResponse - Using Provider: {_parentStatefulComponent.SelectedProvider}");

                    Debug.WriteLine("[AIWorkerBase] Number of messages: " + messages.Count);

                    if (messages == null || !messages.Any())
                    {
                        return null;
                    }

                    // Get endpoint based on component type
                    string endpoint = _parentStatefulComponent.GetEndpoint();

                    var response = await AIUtils.GetResponse(
                        _parentStatefulComponent.SelectedProvider,
                        _parentStatefulComponent.GetModel(),
                        messages,
                        endpoint: endpoint);

                    _parentStatefulComponent.StoreResponseMetrics(response);

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
        }
    }
}
