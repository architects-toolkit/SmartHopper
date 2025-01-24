/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Base class for all AI-powered stateful asynchronous SmartHopper components.
 * This class provides the fundamental structure for components that
 * need to perform asynchronous AI queries, showing an state message,
 * while maintaining Grasshopper's component lifecycle.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Configuration;
using SmartHopper.Config.Models;
using SmartHopper.Config.Providers;
using SmartHopper.Core.Utils;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities.
    /// </summary>
    public abstract class AIStatefulAsyncComponentBase : StatefulAsyncComponentBase
    {
        /// <summary>
        /// The model to use for AI processing. Set up from the component's inputs.
        /// </summary>
        protected string _model { get; private set; }

        /// <summary>
        /// The selected AI provider. Set up from the component's dropdown menu.
        /// </summary>
        protected string _aiProvider { get; private set; }
        private string _previousSelectedProvider;

        /// <summary>
        /// Creates a new instance of the AI-powered stateful asynchronous component.
        /// </summary>
        /// <param name="name">The component's display name</param>
        /// <param name="nickname">The component's nickname</param>
        /// <param name="description">Description of the component's functionality</param>
        /// <param name="category">Category in the Grasshopper toolbar</param>
        /// <param name="subCategory">Subcategory in the Grasshopper toolbar</param>
        protected AIStatefulAsyncComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            _aiProvider = MistralAI._name; // Default to MistralAI
        }

        #region I/O

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Allow derived classes to add their specific inputs
            RegisterAdditionalInputParams(pManager);

            pManager.AddTextParameter("Model", "M", "Specify the name of the AI model to use, in the format specified by the provider.\nIf none is specified, the default model will be used.\nYou can define the default model in the SmartHopper settings menu.", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Run?", "R", "Set this parameter to true to run the component.", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers output parameters for the component.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Allow derived classes to add their specific outputs
            base.RegisterOutputParams(pManager);

            pManager.AddTextParameter("Metrics", "M", "Usage metrics in JSON format including input tokens, output tokens, and completion time.", GH_ParamAccess.item);
        }

        #endregion

        #region LIFECYCLE

        /// <summary>
        /// Main solving method for the component.
        /// Handles the execution flow and persistence of results.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        /// <remarks>
        /// This method is sealed to ensure proper persistence and error handling.
        /// Override OnSolveInstance for custom solving logic.
        /// </remarks>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string model = null;
            DA.GetData("Model", ref model);
            SetModel(model);

            base.SolveInstance(DA);
        }

        #endregion

        #region PROVIDER

        /// <summary>
        /// Appends additional menu items to the component's context menu.
        /// </summary>
        /// <param name="menu">The menu to append to.</param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // Add provider selection submenu
            var providersMenu = new ToolStripMenuItem("Select AI Provider");
            menu.Items.Add(providersMenu);

            // Get all available providers
            var providers = SmartHopperSettings.DiscoverProviders();
            foreach (var provider in providers)
            {
                var item = new ToolStripMenuItem(provider.Name)
                {
                    Checked = provider.Name == _aiProvider,
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

                        _aiProvider = menuItem.Tag.ToString();
                        ExpireSolution(true);
                    }
                };

                providersMenu.DropDownItems.Add(item);
            }
        }

        /// <summary>
        /// Sets the model to use for AI processing.
        /// </summary>
        /// <param name="model">The model to use</param>
        protected void SetModel(string model)
        {
            _model = model;
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <returns>The model to use, or empty string for default model</returns>
        protected string GetModel()
        {
            return _model ?? ""; // "" means that the provider will use the default model
        }

        /// <summary>
        /// Gets the API's endpoint to use when getting AI responses.
        /// </summary>
        /// <returns>The API's endpoint, or empty string for default endpoint</returns>
        protected string GetEndpoint()
        {
            return ""; // "" means that the provider will use the default endpoint
        }

        protected override List<string> InputsChanged()
        {
            List<string> changedInputs = base.InputsChanged();

            if (_aiProvider != _previousSelectedProvider)
            {
                changedInputs.Add("AIProvider");
                _previousSelectedProvider = _aiProvider;
            }

            return changedInputs;
        }

        #endregion

        #region AI

        /// <summary>
        /// Gets a response from the AI provider using the provided messages and cancellation token.
        /// </summary>
        /// <param name="messages">The messages to send to the AI provider.</param>
        /// <param name="token">The cancellation token to cancel the operation.</param>
        /// <returns>The AI response from the provider.</returns>
        protected async Task<AIResponse> GetResponse(List<KeyValuePair<string, string>> messages, CancellationToken token)
        {
            //if (_isDebouncing)
            //{
            //    Debug.WriteLine("[AIStatefulAsyncComponentBase] [GetResponse] Debouncing, skipping request");
            //    return new AIResponse
            //    {
            //        Response = "Too many requests, please wait...",
            //        FinishReason = "error",
            //        InTokens = 0,
            //        OutTokens = 0
            //    };
            //}

            try
            {
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [GetResponse] Using Provider: {_aiProvider}");

                Debug.WriteLine("[AIStatefulAsyncComponentBase] Number of messages: " + messages.Count);

                if (messages == null || !messages.Any())
                {
                    return null;
                }

                var response = await AIUtils.GetResponse(
                    _aiProvider,
                    model: GetModel(),
                    messages,
                    endpoint: GetEndpoint());

                StoreResponseMetrics(response);

                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] GetResponse - Exception: {ex.Message}");
                return new AIResponse
                {
                    Response = ex.Message,
                    FinishReason = "error"
                };
            }
        }

        #endregion

        #region METRICS

        /// <summary>
        /// List of AI response metrics.
        /// </summary>
        private List<AIResponse> _responseMetrics = new List<AIResponse>();

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
                new JProperty("ai_provider", _aiProvider),
                new JProperty("ai_model", _model),
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

        #endregion
    }
}