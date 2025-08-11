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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities
    /// with AI provider selection functionality.
    /// </summary>
    public abstract class AIStatefulAsyncComponentBase : AIProviderComponentBase
    {
        /// <summary>
        /// The model to use for AI processing. Set up from the component's inputs.
        /// </summary>
        private string _model;

        /// <summary>
        /// AI metrics from the last call.
        /// </summary>
        private AIMetrics _responseMetrics;

        /// <summary>
        /// Number of iterations performed.
        /// </summary>
        private int _iterationsCount;

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
            this.SetModel(model);

            base.SolveInstance(DA);
        }

        #endregion

        #region PROVIDER

        // Provider selection functionality is now inherited from AIProviderComponentBase

        /// <summary>
        /// Sets the model to use for AI processing.
        /// </summary>
        /// <param name="model">The model to use</param>
        protected void SetModel(string model)
        {
            this._model = model;
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <returns>The model to use, or empty string for default model</returns>
        protected string GetModel()
        {
            // Get the model, using provider settings default if empty
            string model = this._model;
            var provider = this.GetActualAIProvider();
            if (provider == null)
            {
                // Handle null provider scenario, return default model
                return string.Empty;
            }

            string actualModel = provider.Models.GetModel(model);

            return actualModel;
        }

        #endregion

        #region AI

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

            // Provider and model
            var providerName = this.GetActualAIProviderName();
            var model = this.GetModel();

            AIToolCall toolCall = new AIToolCall
            {
                Name = toolName,
                Arguments = parameters,
                Provider = providerName,
                Model = model,
            };

            toolCall.ReplaceReuseCount(reuseCount);

            // Surface validation messages from AIToolCall/AIRequestBase validation
            try
            {
                var (isValid, messages) = toolCall.IsValid();
                if (messages != null && messages.Count > 0)
                {
                    int idx = 0;
                    foreach (var msg in messages)
                    {
                        idx++;
                        var level = msg != null && msg.StartsWith("(Info)", StringComparison.OrdinalIgnoreCase)
                            ? GH_RuntimeMessageLevel.Remark
                            : GH_RuntimeMessageLevel.Error;
                        var message = msg.StartsWith("(Info)", StringComparison.OrdinalIgnoreCase) ? msg.Replace("(Info) ", "", StringComparison.OrdinalIgnoreCase) : msg;
                        this.SetPersistentRuntimeMessage($"tool_validation_{idx}", level, message, false);
                    }
                }
            }
            catch (Exception valEx)
            {
                // Log validation message surfacing issues but do not fail execution
                Debug.WriteLine($"[AIStatefulAsyncComponentBase] Validation message processing error: {valEx.Message}");
            }

            JObject result;

            try
            {
                result = await AIToolManager
                    .ExecuteTool(toolCall)
                    .ConfigureAwait(false) as JObject;
            }
            catch (Exception ex)
            {
                // Execution error
                this.SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    ex.Message,
                    false);
                result = new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message,
                };
            }

            // Store metrics if present
            // TODO: use AIMetrics
            if (result.TryGetValue("rawResponse", out var metricsToken))
            {
                var aiResp = metricsToken.ToObject<AIResponse>();
                StoreResponseMetrics(aiResp);
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

        /// <summary>
        /// Converts an AI response to a persistent runtime message.
        /// </summary>
        /// <param name="response">The AI response to convert.</param>
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
        /// Stores the given AI response metrics in the component's internal metrics list.
        /// </summary>
        /// <param name="response">The AI response to store metrics from.</param>
        /// <param name="reuseCount">Optional. The number of times this response is reused across different branches. Default is 1.</param>
        public void StoreResponseMetrics(AIReturn toolReturn)
        {
            if (toolReturn != null)
            {
                var metrics = toolReturn.Metrics;

                _responseMetrics.Combine(metrics);
                _iterationsCount++;

                Debug.WriteLine($"[AIStatefulAsyncComponentBase] [StoreResponseMetrics] Added response to metrics list with reuse count: {metrics.ReuseCount}");
            }
        }

        /// <summary>
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason)
        /// </summary>
        /// <param name="DA">The data access object</param>
        protected void SetMetricsOutput(IGH_DataAccess DA)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            if (_responseMetrics == null)
            {
                Debug.WriteLine("[AIStatefulComponentBase] Empty metrics, skipping");
                return;
            }

            // Get the actual provider name
            string actualProvider = GetActualAIProviderName();

            // Aggregate metrics
            int totalInTokens = this._responseMetrics.InputTokens;
            int totalOutTokens = this._responseMetrics.OutputTokens;
            string finishReason = this._responseMetrics.FinishReason;
            double totalCompletionTime = this._responseMetrics.CompletionTime;
            string usedModel = this._responseMetrics.Model;

            // Create JSON object with metrics
            var metricsJson = new JObject(
                new JProperty("ai_provider", actualProvider),
                new JProperty("ai_model", usedModel),
                new JProperty("tokens_input", totalInTokens),
                new JProperty("tokens_output", totalOutTokens),
                new JProperty("finish_reason", finishReason),
                new JProperty("completion_time", totalCompletionTime),
                new JProperty("data_count", _responseMetrics.ReuseCount),
                new JProperty("iterations_count", _iterationsCount)
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
            // Fix for boolean toggle issue: Only clear when Run is true AND we have no active workers
            // This ensures we're truly starting a new processing run, not in the middle of one
            if (this.CurrentState == ComponentState.Processing && this.Run && this.Workers.Count == 0)
            {
                Debug.WriteLine("[AIStatefulAsyncComponentBase] Cleaning previous response metrics for new Processing run");
                _responseMetrics.Clear();
                _iterationsCount = 0;
            }
        }

        protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
            SetMetricsOutput(DA);
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

    }
}
