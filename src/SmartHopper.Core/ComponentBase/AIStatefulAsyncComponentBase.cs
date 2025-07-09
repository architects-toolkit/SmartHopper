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
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Managers.AITools;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Core.AI;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities.
    /// </summary>
    public abstract class AIStatefulAsyncComponentBase : StatefulAsyncComponentBase
    {
        /// <summary>
        /// Special value used to indicate that the default provider from settings should be used.
        /// </summary>
        public const string DEFAULT_PROVIDER = "Default";

        /// <summary>
        /// The model to use for AI processing. Set up from the component's inputs.
        /// </summary>
        private string _model;

        /// <summary>
        /// The selected AI provider. Set up from the component's dropdown menu.
        /// </summary>
        public string _aiProvider { get; private set; }
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
            // Set the default provider option
            _aiProvider = DEFAULT_PROVIDER;
        }

        #region PARAMS

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

            // Add the Default option first
            var defaultItem = new ToolStripMenuItem(DEFAULT_PROVIDER)
            {
                Checked = _aiProvider == DEFAULT_PROVIDER,
                CheckOnClick = true,
                Tag = DEFAULT_PROVIDER
            };

            defaultItem.Click += (s, e) =>
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

                    _aiProvider = DEFAULT_PROVIDER;
                    ExpireSolution(true);
                }
            };

            providersMenu.DropDownItems.Add(defaultItem);

            // Get all available providers
            var providers = ProviderManager.Instance.GetProviders();
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

        //TODO: deprecate GetResponse, replace with CallAiTool to handle provider and model selection, as well as metrics output

        /// <summary>
        /// Gets a response from the AI provider using the provided messages and cancellation token.
        /// </summary>
        /// <param name="messages">The messages to send to the AI provider.</param>
        /// <param name="contextProviderFilter">Optional filter for context providers (comma-separated list).</param>
        /// <param name="contextKeyFilter">Optional filter for context keys (comma-separated list).</param>
        /// <param name="reuseCount">Optional. The number of times this response will be reused across different branches. Default is 1.</param>
        /// <returns>The AI response from the provider.</returns>
        protected async Task<AIResponse> GetResponse(
            List<KeyValuePair<string, string>> messages,
            string contextProviderFilter = null,
            string contextKeyFilter = null,
            int reuseCount = 1)
        {
            try
            {
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [GetResponse] This method is being deprecated. Use CallAiToolAsync instead.");
                
                // Get the actual provider name to use
                string actualProvider = GetActualProviderName();
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [GetResponse] Using Provider: {actualProvider} (Selected: {_aiProvider})");

                Debug.WriteLine("[AIStatefulAsyncComponentBase] Number of messages: " + messages.Count);

                if (messages == null || !messages.Any())
                {
                    return null;
                }

                var response = await AIUtils.GetResponse(
                    actualProvider,
                    model: GetModel(),
                    messages,
                    contextProviderFilter: contextProviderFilter,
                    contextKeyFilter: contextKeyFilter);

                // Store response metrics with the provided reuse count
                StoreResponseMetrics(response, reuseCount);

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

        /// <summary>
        /// Executes an AI tool via AIToolManager, auto-injecting provider/model
        /// and storing returned metrics.
        /// </summary>
        /// <param name="toolName">Name of the registered tool.</param>
        /// <param name="parameters">Tool-specific parameters; provider/model will be injected.</param>
        /// <param name="reuseCount">Reuse count for metrics accounting.</param>
        /// <returns>Raw tool result as JObject.</returns>
        protected async Task<JObject> CallAiToolAsync(string toolName, JObject parameters, int reuseCount = 1)
        {
            parameters ??= new JObject();
            // Inject provider and model
            parameters["provider"] = GetActualProviderName();
            parameters["model"]    = GetModel();
            parameters["reuseCount"] = reuseCount;

            JObject result;
            try
            {
                result = await AIToolManager
                    .ExecuteTool(toolName, parameters, null)
                    .ConfigureAwait(false) as JObject;
            }
            catch (Exception ex)
            {
                // Execution error
                SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    ex.Message,
                    false);
                result = new JObject
                {
                    ["success"] = false,
                    ["error"]   = ex.Message
                };
            }

            // Store metrics if present
            if (result.TryGetValue("rawResponse", out var metricsToken))
            {
                var aiResp = metricsToken.ToObject<AIResponse>();
                StoreResponseMetrics(aiResp, reuseCount);
            }

            // Handle tool-level failure
            bool ok = result.Value<bool?>("success") ?? true;
            if (!ok)
            {
                var errorMsg = result.Value<string>("error") ?? "Unknown error occurred";
                SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    errorMsg,
                    false);
            }

            return result;
        }

        protected void AIErrorToPersistentRuntimeMessage(AIResponse response)
        {
            var responseMessage = response.Response.ToLower();

            if (responseMessage.Contains("401") ||
                responseMessage.Contains("unauthorized"))
            {
                SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    $"AUTHENTICATION ERROR: is the API key correct?",
                    false
                );
            }
            else
            {
                SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    $"AI error while processing the response:\n{response.Response}",
                    false
                );
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
        /// <param name="reuseCount">Optional. The number of times this response is reused across different branches. Default is 1.</param>
        public void StoreResponseMetrics(AIResponse response, int reuseCount = 1)
        {
            if (response != null)
            {
                // Set the reuse count on the response
                response.ReuseCount = reuseCount;

                _responseMetrics.Add(response);

                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [StoreResponseMetrics] Added response to metrics list with reuse count: {reuseCount}");
            }
        }

        /// <summary>
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason)
        /// </summary>
        /// <param name="DA">The data access object</param>
        protected void SetMetricsOutput(IGH_DataAccess DA)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            if (!_responseMetrics.Any())
            {
                Debug.WriteLine("[AIStatefulComponentBase] Empty metrics, skipping");
                return;
            }

            // Get the actual provider name
            string actualProvider = GetActualProviderName();

            // Aggregate metrics
            int totalInTokens = _responseMetrics.Sum(r => r.InTokens);
            int totalOutTokens = _responseMetrics.Sum(r => r.OutTokens);
            string finishReason = _responseMetrics.Last().FinishReason;
            double totalCompletionTime = _responseMetrics.Sum(r => r.CompletionTime);
            string usedModels = string.Join(", ", _responseMetrics
                .Select(r => r.Model)
                .Distinct());

            // Create JSON object with metrics
            var metricsJson = new JObject(
                new JProperty("ai_provider", actualProvider),
                new JProperty("ai_model", usedModels),
                new JProperty("tokens_input", totalInTokens),
                new JProperty("tokens_output", totalOutTokens),
                new JProperty("finish_reason", finishReason),
                new JProperty("completion_time", totalCompletionTime),
                new JProperty("data_count", _responseMetrics.Sum(r => r.ReuseCount)),
                new JProperty("iterations_count", _responseMetrics.Count)
            );

            // Convert metricsJson to GH_String
            var metricsJsonString = metricsJson.ToString();
            var ghString = new GH_String(metricsJsonString);

            // Set the metrics output
            SetPersistentOutput("Metrics", ghString, DA);

            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsJson}");
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // Clear previous response metrics only when starting a new run
            if (this.CurrentState == ComponentState.Processing && this.Run)
            {
                Debug.WriteLine("[AIStatefulAsyncComponentBase] Cleaning previous response metrics");

                // Clear the stored metrics on start a new run
                _responseMetrics.Clear();
            }
        }

        protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
            SetMetricsOutput(DA);
        }

        #endregion

        #region DESIGN

        /// <summary>
        /// Creates the custom attributes for this component, which includes the provider logo badge.
        /// </summary>
        public override void CreateAttributes()
        {
            m_attributes = new AIComponentAttributes(this);
        }

        #endregion

        #region TYPE

        protected static GH_Structure<GH_String> ConvertToGHString(GH_Structure<IGH_Goo> tree)
        {
            var stringTree = new GH_Structure<GH_String>();
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                var stringBranch = new List<GH_String>();
                foreach (var item in branch)
                {
                    stringBranch.Add(new GH_String(item.ToString()));
                }
                stringTree.AppendRange(stringBranch, path);
            }

            return stringTree;
        }

        #endregion

        #region PERSISTENCE

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization</param>
        /// <returns>True if the write operation succeeds, false if it fails or an exception occurs</returns>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer))
                return false;

            try
            {
                // Store the selected AI provider
                writer.SetString("AIProvider", _aiProvider);
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [Write] Stored AI provider: {_aiProvider}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [Write] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the component's persistent data from the Grasshopper file.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization</param>
        /// <returns>True if the read operation succeeds, false if it fails, required data is missing, or an exception occurs</returns>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader))
                return false;

            try
            {
                // Read the stored AI provider if available
                if (reader.ItemExists("AIProvider"))
                {
                    string storedProvider = reader.GetString("AIProvider");
                    Debug.WriteLine($"[AIStatefulAsyncComponentBase] [Read] Read stored AI provider: {storedProvider}");

                    // Check if the provider exists in the available providers
                    var providers = ProviderManager.Instance.GetProviders();
                    if (providers.Any(p => p.Name == storedProvider))
                    {
                        _aiProvider = storedProvider;
                        _previousSelectedProvider = storedProvider;
                        Debug.WriteLine($"[AIStatefulAsyncComponentBase] [Read] Restored AI provider: {_aiProvider}");
                    }
                    else
                    {
                        // If the provider doesn't exist, use the first available provider
                        var availableProviders = ProviderManager.Instance.GetProviders();
                        _aiProvider = availableProviders.Any() ? availableProviders.First().Name : "Default";
                        _previousSelectedProvider = _aiProvider;
                        Debug.WriteLine($"[AIStatefulAsyncComponentBase] [Read] Provider not found, using default: {_aiProvider}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [Read] Exception: {ex.Message}");
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Gets the actual provider name to use for AI processing.
        /// If the selected provider is "Default", returns the default provider from settings.
        /// </summary>
        /// <returns>The actual provider name to use</returns>
        protected string GetActualProviderName()
        {
            if (_aiProvider == DEFAULT_PROVIDER)
            {
                // Use the ProviderManager to get the default provider
                return ProviderManager.Instance.GetDefaultAIProvider();
            }

            return _aiProvider;
        }
    }
}
