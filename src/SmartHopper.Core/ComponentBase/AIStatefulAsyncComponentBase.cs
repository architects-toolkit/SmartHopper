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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Settings;

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
        private string model;

        /// <summary>
        /// Last AI return snapshot stored by this component.
        /// </summary>
        private AIReturn AIReturnSnapshot;
        
        // Flag to ensure we only clear metrics once per new Processing run
        private bool metricsInitializedForRun;

        /// <summary>
        /// Cached badge flags (to prevent recomputation during Render/panning).
        /// </summary>
        private bool badgeVerified;
        private bool badgeDeprecated;
        private bool badgeCacheValid;
        private bool badgeInvalidModel;
        private bool badgeReplacedModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIStatefulAsyncComponentBase"/> class.
        /// Creates a new instance of the AI-powered stateful asynchronous component.
        /// </summary>
        /// <param name="name">The component's display name.</param>
        /// <param name="nickname">The component's nickname.</param>
        /// <param name="description">Description of the component's functionality.</param>
        /// <param name="category">Category in the Grasshopper toolbar.</param>
        /// <param name="subCategory">Subcategory in the Grasshopper toolbar.</param>
        protected AIStatefulAsyncComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
        }

        /// <summary>
        /// Required capability for this component. Derived components should override to specify
        /// the exact capability they need (e.g., Text2Image, ToolChat, etc.). Defaults to Text2Text.
        /// </summary>
        protected virtual AICapability RequiredCapability => AICapability.Text2Text;

        #region PARAMS

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Allow derived classes to add their specific inputs
            this.RegisterAdditionalInputParams(pManager);

            pManager.AddTextParameter("Model", "M", "Specify the name of the AI model to use, in the format specified by the provider.\nIf none is specified, the default model will be used.\nYou can define the default model in the SmartHopper settings menu.", GH_ParamAccess.item, string.Empty);
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
            string? model = null;
            DA.GetData("Model", ref model);
            this.SetModel(model);

            // Update badge cache using current inputs before executing the solution
            this.UpdateBadgeCache();

            base.SolveInstance(DA);
        }

        #endregion

        #region PROVIDER

        // Provider selection functionality is now inherited from AIProviderComponentBase

        /// <summary>
        /// Sets the model to use for AI processing.
        /// </summary>
        /// <param name="model">The model to use.</param>
        protected void SetModel(string model)
        {
            this.model = model;
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <returns>The model to use, or empty string for default model.</returns>
        protected string GetModel()
        {
            // Get the model, using provider settings default if empty
            string model = this.model;
            var provider = this.GetActualAIProvider();
            if (provider == null)
            {
                // Handle null provider scenario, return default model
                return string.Empty;
            }

            // If user specified a model, pass it through
            if (!string.IsNullOrWhiteSpace(model))
            {
                return model;
            }

            // Otherwise, use provider-level default resolution (respects settings and capabilities)
            var selected = provider.GetDefaultModel(this.RequiredCapability, useSettings: true);
            return selected ?? string.Empty;
        }

        #endregion

        #region AI

        /// <summary>
        /// Executes an AI tool via AIToolManager, auto-injecting provider/model
        /// and storing returned metrics.
        /// </summary>
        /// <param name="toolName">Name of the registered tool.</param>
        /// <param name="parameters">Tool-specific parameters; provider/model will be injected.</param>
        /// <returns>Raw tool result as JObject.</returns>
        protected async Task<JObject> CallAiToolAsync(string toolName, JObject parameters)
        {
            parameters ??= new JObject();

            // Provider and model
            var providerName = this.GetActualAIProviderName();
            var model = this.GetModel();

            // Create the tool call interaction with proper structure
            var toolCallInteraction = new AIInteractionToolCall
            {
                Name = toolName,
                Arguments = parameters,
                Agent = AIAgent.Assistant,
            };

            // Create the tool call request with proper body
            var toolCall = new AIToolCall();
            toolCall.Provider = providerName;
            toolCall.Model = model;
            toolCall.Endpoint = toolName;
            var immutableBody = AIBodyBuilder.Create()
                .Add(toolCallInteraction)
                .Build();
            toolCall.Body = immutableBody;

            // Validation/capability messages will be surfaced from AIReturn after execution

            AIReturn toolResult;
            JObject result;

            try
            {
                toolResult = await toolCall.Exec().ConfigureAwait(false);

                // Extract the result from the AIReturn
                var toolResultInteraction = toolResult.Body.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (toolResultInteraction?.Result != null)
                {
                    result = toolResultInteraction.Result;
                }
                else
                {
                    result = new JObject
                    {
                        ["success"] = toolResult.Success,
                        ["error"] = toolResult.ErrorMessage
                    };
                }
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
                toolResult = null;
            }

            // Store full AIReturn for downstream metrics/output usage
            if (toolResult != null)
            {
                this.AIReturnSnapshot = toolResult;
            }

            // Surface propagated validation/capability messages from AIReturn
            if (toolResult?.Messages != null && toolResult.Messages.Count > 0)
            {
                this.SurfaceMessagesFromReturn(toolResult, "ai");
            }

            // Handle tool-level failure
            bool ok = result.Value<bool?>("success") ?? true;
            if (!ok)
            {
                var errorMsg = result.Value<string>("error") ?? toolResult?.ErrorMessage;
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    this.SetPersistentRuntimeMessage(
                        "ai_error",
                        GH_RuntimeMessageLevel.Error,
                        errorMsg,
                        false);
                }
            }

            return result;
        }

        #endregion

        #region METRICS

        /// <summary>
        /// Replaces the internal last AIReturn snapshot.
        /// Use this when an external source (e.g., WebChat) provides the full response state.
        /// </summary>
        /// <param name="ret">AIReturn snapshot to store.</param>
        public virtual void SetAIReturnSnapshot(AIReturn ret)
        {
            if (ret == null)
            {
                return;
            }

            this.AIReturnSnapshot = ret;
        }

        /// <summary>
        /// Gets the current <see cref="AIReturn"/> snapshot stored in this component.
        /// Intended for derived components that need to render outputs (e.g., chat history)
        /// from the same source of truth used for metrics.
        /// </summary>
        /// <returns>The current AIReturn snapshot, or null if none.</returns>
        protected AIReturn GetAIReturnSnapshot()
        {
            return this.AIReturnSnapshot;
        }

        /// <summary>
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason).
        /// </summary>
        /// <param name="dA">The data access object.</param>
        protected void SetMetricsOutput(IGH_DataAccess dA)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            var metrics = this.AIReturnSnapshot?.Metrics;
            if (metrics == null)
            {
                Debug.WriteLine("[AIStatefulComponentBase] Empty metrics, skipping");
                return;
            }

            // Create JSON object with metrics
            var metricsJson = new JObject(
                new JProperty("ai_provider", metrics.Provider),
                new JProperty("ai_model", metrics.Model),
                new JProperty("tokens_input", metrics.InputTokens),
                new JProperty("tokens_output", metrics.OutputTokens),
                new JProperty("finish_reason", metrics.FinishReason),
                new JProperty("completion_time", metrics.CompletionTime),
                new JProperty("data_count", this.dataCount),
                new JProperty("iterations_count", this.ProgressInfo.Total));

            // Convert metricsJson to GH_String
            var metricsJsonString = metricsJson.ToString();
            var ghString = new GH_String(metricsJsonString);

            // Set the metrics output
            this.SetPersistentOutput("Metrics", ghString, dA);

            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsJson}");
        }

        /// <summary>
        /// Controls whether the base class should emit metrics during the post-solve phase.
        /// Derived classes can override to return false when they set metrics synchronously
        /// within their own SolveInstance implementation.
        /// </summary>
        protected virtual bool ShouldEmitMetricsInPostSolve()
        {
            return true;
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // Reset the one-time init flag when not processing
            if (this.CurrentState != ComponentState.Processing)
            {
                this.metricsInitializedForRun = false;
            }

            // Clear previous response metrics only once when truly starting a new Processing run
            // Conditions:
            //  - In Processing state
            //  - Run is true
            //  - No active workers yet (fresh run)
            //  - Not already initialized for this run (prevents repeated clears on multiple presolves)
            if (this.CurrentState == ComponentState.Processing && this.Run && this.Workers.Count == 0 && !this.metricsInitializedForRun)
            {
                Debug.WriteLine("[AIStatefulAsyncComponentBase] Cleaning previous response metrics for new Processing run");
                this.AIReturnSnapshot = null;
                this.metricsInitializedForRun = true;
            }
        }

        protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
            if (this.ShouldEmitMetricsInPostSolve())
            {
                this.SetMetricsOutput(DA);
            }

            // Update badge cache again after solving, so last metrics model is considered
            this.UpdateBadgeCache();
        }

        #endregion

        #region UI

        /// <summary>
        /// Creates the custom attributes for this component, enabling provider and model badges.
        /// Uses <see cref="ComponentBadgesAttributes"/> to render provider icon (via base) and model state badges.
        /// </summary>
        public override void CreateAttributes()
        {
            this.m_attributes = new ComponentBadgesAttributes(this);
        }

        /// <summary>
        /// Updates the cached badge flags based on the most relevant model and provider.
        /// Priority for model: last metrics model, then configured/default model.
        /// </summary>
        internal void UpdateBadgeCache()
        {
            try
            {
                // Resolve provider
                string providerName = this.GetActualAIProviderName();
                if (providerName == AIProviderComponentBase.DEFAULT_PROVIDER)
                {
                    providerName = SmartHopperSettings.Instance.DefaultAIProvider;
                }

                // Resolve model the user currently configured (for validation/replacement decisions)
                string configuredModel = this.GetModel();

                // If provider is missing, we cannot resolve anything
                if (string.IsNullOrWhiteSpace(providerName))
                {
                    this.badgeVerified = false;
                    this.badgeDeprecated = false;
                    this.badgeInvalidModel = true;
                    this.badgeReplacedModel = false;
                    this.badgeCacheValid = false;
                    return;
                }

                // Build a minimal request to leverage centralized validation/model selection
                // Use a dummy endpoint and a single system interaction to satisfy validators
                var interactions = new List<IAIInteraction>
                {
                    new AIInteractionText { Agent = AIAgent.System, Content = "badge-check" },
                };

                var req = new SmartHopper.Infrastructure.AICall.Core.Requests.AIRequestCall();
                req.Initialize(providerName, configuredModel ?? string.Empty, interactions, endpoint: "badge_check", capability: this.RequiredCapability, toolFilter: null);

                // This triggers provider-scoped selection/fallback based on capability
                string resolvedModel = req.Model;

                // Gather validation messages (may include provider/model issues)
                var (isValid, validationMessages) = req.IsValid();

                // Prefer structured codes; fall back to text checks when Code is Unknown
                bool hasProviderMissing = validationMessages?.Any(m =>
                    m.Severity == AIRuntimeMessageSeverity.Error &&
                    (m.Code == AIMessageCode.ProviderMissing)) == true;

                bool hasUnknownProvider = validationMessages?.Any(m =>
                    m.Severity == AIRuntimeMessageSeverity.Error &&
                    (m.Code == AIMessageCode.UnknownProvider)) == true;

                bool hasNoCapableModel = validationMessages?.Any(m =>
                    m.Severity == AIRuntimeMessageSeverity.Error &&
                    (m.Code == AIMessageCode.NoCapableModel)) == true;

                bool hasUnknownModel = validationMessages?.Any(m =>
                    (m.Code == AIMessageCode.UnknownModel)) == true;

                bool hasCapabilityMismatch = validationMessages?.Any(m =>
                    (m.Code == AIMessageCode.CapabilityMismatch)) == true;

                // Replaced when selection adjusted or an explicit CapabilityMismatch is present
                this.badgeReplacedModel = (!string.IsNullOrWhiteSpace(configuredModel)
                                           && !string.IsNullOrWhiteSpace(resolvedModel)
                                           && !string.Equals(configuredModel, resolvedModel, StringComparison.Ordinal))
                                          || hasCapabilityMismatch;

                // Invalid when missing/unknown provider, unknown model, no capable model, capability mismatch, or empty configured model
                this.badgeInvalidModel = string.IsNullOrWhiteSpace(configuredModel)
                                         || hasProviderMissing
                                         || hasUnknownProvider
                                         || hasUnknownModel
                                         || hasNoCapableModel
                                         || hasCapabilityMismatch
                                         || this.badgeReplacedModel;

                // Read metadata from the resolved model to set Verified/Deprecated when available
                var resolvedCaps = string.IsNullOrWhiteSpace(resolvedModel) ? null : ModelManager.Instance.GetCapabilities(providerName, resolvedModel);
                if (resolvedCaps == null)
                {
                    // No metadata available for the resolved model – do not render badges
                    this.badgeVerified = false;
                    this.badgeDeprecated = false;
                    this.badgeCacheValid = true;
                }
                else
                {
                    // Verified/Deprecated reflect the model actually selected for execution; Verified requires capability match
                    this.badgeVerified = resolvedCaps.Verified && resolvedCaps.HasCapability(this.RequiredCapability);
                    this.badgeDeprecated = resolvedCaps.Deprecated;
                    this.badgeCacheValid = true;
                }

                return;
            }
            catch
            {
                // On any failure, mark cache invalid to avoid rendering
                this.badgeVerified = false;
                this.badgeDeprecated = false;
                this.badgeInvalidModel = false;
                this.badgeReplacedModel = false;
                this.badgeCacheValid = false;
            }
        }

        /// <summary>
        /// Tries to get the cached badge flags without recomputation.
        /// </summary>
        /// <param name="verified">True if model is verified.</param>
        /// <param name="deprecated">True if model is deprecated.</param>
        /// <returns>True if cache is valid; otherwise false.</returns>
        internal bool TryGetCachedBadgeFlags(out bool verified, out bool deprecated)
        {
            verified = this.badgeVerified;
            deprecated = this.badgeDeprecated;
            return this.badgeCacheValid;
        }

        /// <summary>
        /// Tries to get the cached badge flags including invalid and replaced, without recomputation.
        /// </summary>
        /// <param name="verified">True if model is verified and capable.</param>
        /// <param name="deprecated">True if model is deprecated.</param>
        /// <param name="invalid">True if model is unknown or not capable of the required capability.</param>
        /// <param name="replaced">True if the selected model would be replaced by a fallback due to capability mismatch.</param>
        /// <returns>True if cache is valid; otherwise false.</returns>
        internal bool TryGetCachedBadgeFlags(out bool verified, out bool deprecated, out bool invalid, out bool replaced)
        {
            verified = this.badgeVerified;
            deprecated = this.badgeDeprecated;
            invalid = this.badgeInvalidModel;
            replaced = this.badgeReplacedModel;
            return this.badgeCacheValid;
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

        /// <summary>
        /// Surfaces structured runtime messages contained in an <see cref="IAIReturn"/> as persistent
        /// Grasshopper runtime messages. Maps <see cref="AIRuntimeMessageSeverity"/> to
        /// <see cref="GH_RuntimeMessageLevel"/> and prefixes the text with the message <see cref="AIRuntimeMessage.Origin"/>.
        /// </summary>
        /// <param name="aiReturn">The AI return object containing messages.</param>
        /// <param name="keyPrefix">A key prefix to namespace the persistent message keys.</param>
        private void SurfaceMessagesFromReturn(IAIReturn aiReturn, string keyPrefix)
        {
            if (aiReturn?.Messages == null || aiReturn.Messages.Count == 0)
            {
                return;
            }

            int idx = 0;
            foreach (var item in aiReturn.Messages)
            {
                idx++;

                // Only surface messages intended for end users
                if (item == null || !item.Surfaceable)
                {
                    continue;
                }

                // Map structured severity to GH level
                GH_RuntimeMessageLevel level = item.Severity switch
                {
                    AIRuntimeMessageSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                    AIRuntimeMessageSeverity.Error => GH_RuntimeMessageLevel.Error,
                    _ => GH_RuntimeMessageLevel.Remark,
                };

                // Include origin for context, then the message text
                var originTag = $"[{item.Origin}] ";
                var msg = (item.Message ?? string.Empty);

                this.SetPersistentRuntimeMessage($"{keyPrefix}_msg_{idx}", level, originTag + msg, false);
            }
        }

    }
}

