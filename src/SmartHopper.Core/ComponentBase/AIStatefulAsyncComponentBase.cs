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

/*
 * Base class for all AI-powered stateful asynchronous SmartHopper components.
 * This class provides the fundamental structure for components that
 * need to perform asynchronous AI queries, showing an state message,
 * while maintaining Grasshopper's component lifecycle.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities
    /// with AI provider selection functionality.
    /// </summary>
    /// <remarks>
    /// <para><b>UNIFIED EXECUTION FLOW</b></para>
    /// <para>
    /// Both the non-batch and batch paths converge at <see cref="FinishResults{T}"/>, which
    /// persists all outputs atomically and emits metrics. The two virtual hooks
    /// <see cref="PrepareInputs"/> and <see cref="TransformOutputs"/> fire at identical logical
    /// positions in both paths.
    /// </para>
    ///
    /// <para><b>NON-BATCH PATH:</b></para>
    /// <code>
    /// DoWorkAsync()
    ///   └── RunProcessingAsync()
    ///         └── DataTreeProcessor.RunAsync()
    ///               └── function(inputs)          [called once per branch/item]
    ///                     1. PrepareInputs(inputs, context)    ← virtual hook
    ///                     2. CallAiToolAsync(...)
    ///                        → real result returned immediately
    ///                     3. (return outputs dict)
    ///         └── FinishResults(primary, ...extras)
    ///               → SetPersistentOutput for each output
    ///               → stamp CompletionTime from _batchCompletionTime (if any)
    ///               → SetMetricsOutput(null)
    ///   └── Worker.SetOutput() → no-op
    ///         |
    /// [Completed State] → RestorePersistentOutputs() → canvas updated
    /// </code>
    ///
    /// <para><b>BATCH PATH:</b></para>
    /// <code>
    /// DoWorkAsync()
    ///   └── RunProcessingAsync()
    ///         └── DataTreeProcessor.RunAsync()
    ///               └── function(inputs)          [called once per branch/item]
    ///                     1. PrepareInputs(inputs, context)    ← virtual hook
    ///                     2. CallAiToolAsync(...)
    ///                        → returns ##SH_BATCH:{customId}## sentinel
    ///         └── TrySubmitBatchAsync()           → submits queue to provider
    ///   └── Worker.SetOutput() → no-op
    ///         |
    /// [Stay in Processing State]
    ///         |
    /// PollBatchStatusAsync() [background timer]
    ///         |
    /// OnBatchCompleted(results, messages)
    ///   └── ProcessBatchResults&lt;T&gt;(decode, messages)
    ///         └── for each sentinel in tree:
    ///               item = decode(customId, resultBody)
    ///               TransformOutputs({primary→item}, context)  ← virtual hook
    ///               extras accumulated per sentinel
    ///         └── SetAIReturnSnapshot(aggregatedMetrics)
    ///         └── FinishResults(primary, ...extras)
    ///               → SetPersistentOutput for each output
    ///               → stamp CompletionTime from _batchCompletionTime
    ///               → SetMetricsOutput(null)
    ///         |
    /// Transition to [Completed State]
    /// ExpireSolution() → RestorePersistentOutputs() → canvas updated
    /// </code>
    ///
    /// <para><b>METRICS VARIANTS</b></para>
    /// <list type="bullet">
    /// <item>
    ///   <b>Standard AI components (Group A):</b> metrics emitted exclusively by
    ///   <see cref="FinishResults{T}"/> (non-batch) or <see cref="ProcessBatchResults{T}"/>→<see cref="FinishResults{T}"/>
    ///   (batch). <c>OnSolveInstancePostSolve</c> does NOT call <c>SetMetricsOutput</c>.
    /// </item>
    /// <item>
    ///   <b>Synchronous-output components (e.g. <c>AIChatComponent</c>):</b> call
    ///   <see cref="SetMetricsOutput"/> directly inside their <c>SolveInstance</c> override with
    ///   the live <c>DA</c> reference. They do not use <see cref="FinishResults{T}"/> and are
    ///   unaffected by the hook pattern.
    /// </item>
    /// </list>
    ///
    /// <para><b>COMPONENT IMPLEMENTATION REQUIREMENTS (standard AI component):</b></para>
    /// <code>
    /// // Worker.DoWorkAsync — non-batch branch must call FinishResults:
    /// var batchSubmitted = await parent.TrySubmitBatchAsync("Result", result, token);
    /// if (!batchSubmitted)
    ///     parent.FinishResults("Result", result["Result"]);
    ///
    /// // Worker.SetOutput — must be a no-op:
    /// public override void SetOutput(IGH_DataAccess DA, out string message)
    ///     => message = string.Empty;
    ///
    /// // OnBatchCompleted — delegate entirely to ProcessBatchResults:
    /// protected override void OnBatchCompleted(IReadOnlyDictionary&lt;string, JObject&gt; results)
    /// {
    ///     var sentinel = this.GetSentinelTree("Result");
    ///     if (results == null || sentinel == null) return;
    ///     this.ProcessBatchResults&lt;GH_String&gt;("Result", sentinel, results,
    ///         (customId, body) =&gt; new GH_String(Decode(body)));
    /// }
    /// </code>
    /// </remarks>
    public abstract class AIStatefulAsyncComponentBase : AIProviderComponentBase
    {
        /// <summary>
        /// Per-request AI parameters (model, temperature, max tokens, extras) from the Settings input.
        /// </summary>
        private AIRequestParameters _requestParameters;

        /// <summary>Active batch submission, or null when not in batch mode.</summary>
        private AIBatchSubmission _batchSubmission;

        /// <summary>
        /// Stores sentinel <see cref="GH_Structure{GH_String}"/> trees keyed by output parameter name.
        /// Populated during batch submission so <see cref="OnBatchCompleted"/> can reconstruct output trees.
        /// </summary>
        private Dictionary<string, object> _sentinelTrees;

        /// <summary>
        /// Queue of (CustomId, Request) pairs collected during a batch-mode component run.
        /// Populated by <see cref="CallAiToolAsync"/> when <see cref="IsBatchRequest"/> is true.
        /// Consumed and cleared by <see cref="SubmitBatchQueueAsync"/>.
        /// </summary>
        private List<(string CustomId, AIRequestCall Request)> _batchQueue;

        /// <summary>
        /// Set of all sentinel custom IDs generated for the current (or most-recent) batch run.
        /// Persisted across file save/reload so <see cref="OnBatchCompleted"/> can reconstruct trees.
        /// </summary>
        private HashSet<string> _batchSentinelIds;

        /// <summary>Timer that fires batch status polls.</summary>
        private Timer _batchPollTimer;

        /// <summary>Guards against concurrent poll calls.</summary>
        private int _batchPollRunning;

        /// <summary>Number of items completed so far in the active batch, for live progress display.</summary>
        private int _batchProgressCompleted;

        /// <summary>Timestamp when the batch was submitted to the provider.</summary>
        private DateTime? _batchStartTime;

        /// <summary>
        /// Wall-clock seconds elapsed from batch submission to completion.
        /// Set by <see cref="PollBatchStatusAsync"/> when the batch finishes and consumed by
        /// <see cref="FinishResults{T}"/> to stamp <see cref="AIMetrics.CompletionTime"/>.
        /// Cleared in <see cref="OnEnteringNeedsRunState"/>.
        /// </summary>
        private double? _batchCompletionTime;

        /// <summary>
        /// Last AI return snapshot stored by this component.
        /// </summary>
        private AIReturn AIReturnSnapshot;

        /// <summary>
        /// Single authoritative metrics instance for this component run.
        /// Set by <see cref="ProcessBatchResults{T}"/> (batch) or <see cref="FinishResults{T}"/> (non-batch)
        /// and read by <see cref="SetMetricsOutput"/>.
        /// Avoids the computed-property trap of <see cref="AIReturn.Metrics"/> which re-aggregates
        /// from interactions on every access, making any mutation a no-op.
        /// </summary>
        private AIMetrics _persistedMetrics;

        /// <summary>
        /// Backing storage for the component's declared required capability before merging
        /// with tool capabilities.
        /// </summary>
        private AICapability requiredCapabilityStorage = AICapability.None;

        /// <summary>
        /// Cached badge flags (to prevent recomputation during Render/panning).
        /// </summary>
        private bool badgeVerified;
        private bool badgeDeprecated;
        private bool badgeCacheValid;
        private bool badgeInvalidModel;
        private bool badgeReplacedModel;
        private bool badgeNotRecommended;

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
        /// The getter returns the effective capability, merging the declared capability with any
        /// capabilities required by tools listed in <see cref="UsingAiTools"/>. The setter updates
        /// the underlying declared capability storage.
        /// </summary>
        protected virtual AICapability RequiredCapability
        {
            get
            {
                var effective = this.requiredCapabilityStorage;
                var toolNames = this.UsingAiTools;
                if (toolNames == null || toolNames.Count == 0)
                {
                    return effective;
                }

                // Ensure tools are discovered
                AIToolManager.DiscoverTools();
                var tools = AIToolManager.GetTools();

                foreach (var toolName in toolNames)
                {
                    if (tools.TryGetValue(toolName, out var tool) && tool.RequiredCapabilities != AICapability.None)
                    {
                        effective |= tool.RequiredCapabilities;
                    }
                }

                return effective;
            }

            set
            {
                this.requiredCapabilityStorage = value;
            }
        }

        /// <summary>
        /// List of AI tool names that this component uses.
        /// Derived classes should override this to specify which AI tools they use.
        /// This is used for:
        /// 1. Merging tool capability requirements into the effective RequiredCapability
        /// 2. Checking if the selected model is discouraged for any of these tools
        /// </summary>
        protected virtual IReadOnlyList<string> UsingAiTools => Array.Empty<string>();

        #region PARAMS

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Allow derived classes to add their specific inputs
            this.RegisterAdditionalInputParams(pManager);

            pManager.AddGenericParameter("Settings", "S", "AI request settings (model, temperature, tokens, extras).\nConnect an AI Settings component or enter a model name as text for quick setup.\nLeave empty to use all provider defaults.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
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
            Grasshopper.Kernel.Types.IGH_Goo rawGoo = null;
            if (DA.GetData("Settings", ref rawGoo) && rawGoo != null)
            {
                var scriptVar = rawGoo.ScriptVariable();
                if (scriptVar is AIRequestParameters p)
                {
                    this.SetParameters(p);
                }
                else if (scriptVar is string s)
                {
                    this.SetParameters(AIRequestParameters.FromModel(s.Trim()));
                }
                else
                {
                    // Try string cast path (GH_String etc.)
                    string fallbackStr = rawGoo.ToString();
                    this.SetParameters(AIRequestParameters.FromModel(fallbackStr?.Trim()));
                }
            }
            else
            {
                this.SetParameters(AIRequestParameters.Empty);
            }

            // Update badge cache using current inputs before executing the solution
            this.UpdateBadgeCache();

            base.SolveInstance(DA);
        }

        #endregion

        #region PROVIDER

        // Provider selection functionality is now inherited from AIProviderComponentBase

        /// <summary>
        /// Sets the AI request parameters from the Settings input.
        /// </summary>
        /// <param name="parameters">The AI request parameters to use.</param>
        protected void SetParameters(AIRequestParameters parameters)
        {
            this._requestParameters = parameters ?? AIRequestParameters.Empty;
        }

        /// <summary>
        /// Gets the current AI request parameters.
        /// </summary>
        /// <returns>The current <see cref="AIRequestParameters"/>, never null.</returns>
        protected AIRequestParameters GetParameters() => this._requestParameters ?? AIRequestParameters.Empty;

        /// <summary>
        /// Gets the resolved model name for AI processing.
        /// If the parameters specify a model, it is returned as-is.
        /// Otherwise the provider's capability-aware default is used.
        /// </summary>
        /// <returns>The model name to use, or empty string for provider default.</returns>
        protected string GetModel()
        {
            var modelFromParams = this._requestParameters?.Model;
            var provider = this.GetActualAIProvider();
            if (provider == null)
            {
                return modelFromParams ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(modelFromParams))
            {
                return modelFromParams;
            }

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
            toolCall.Parameters = this.GetParameters();
            var immutableBody = AIBodyBuilder.Create()
                .Add(toolCallInteraction)
                .Build();
            toolCall.Body = immutableBody;

            // Batch interception: if batch mode is active and the tool supports BuildRequest,
            // queue the request instead of executing it and return a sentinel placeholder.
            if (this.IsBatchRequest())
            {
                var tools = AIToolManager.GetTools();
                if (tools.TryGetValue(toolName, out var batchTool) && batchTool.BuildRequest != null)
                {
                    try
                    {
                        var batchRequest = batchTool.BuildRequest(toolCall);

                        // Validate the request and surface warnings before queuing
                        var (isValid, validationMessages) = batchRequest.IsValid();
                        if (validationMessages?.Count > 0)
                        {
                            var warnings = validationMessages.Where(m => m.Severity == AIRuntimeMessageSeverity.Warning);
                            if (warnings.Any())
                            {
                                foreach (var msg in warnings)
                                {
                                    this.SetPersistentRuntimeMessage(
                                        "batch_val_warning",
                                        GH_RuntimeMessageLevel.Warning,
                                        msg.Message,
                                        false);
                                }

                                Debug.WriteLine($"[AIStatefulAsync] Surfaced {warnings.Count()} validation warnings for batch request");
                            }

                            // Only log errors, do not surface. Call will fail if errors are relevant
                            var errors = validationMessages.Where(m => m.Severity == AIRuntimeMessageSeverity.Error);
                            if (errors.Any())
                            {
                                Debug.WriteLine($"[AIStatefulAsync] Batch request has {errors.Count()} validation errors - proceeding with queuing anyway");
                            }
                        }

                        var index = this._batchQueue?.Count ?? 0;
                        var customId = AIBatchSubmission.GenerateCustomId(toolName, index);

                        if (this._batchQueue == null) this._batchQueue = new List<(string, AIRequestCall)>();
                        if (this._batchSentinelIds == null) this._batchSentinelIds = new HashSet<string>();
                        this._batchQueue.Add((customId, batchRequest));
                        this._batchSentinelIds.Add(customId);

                        Debug.WriteLine($"[AIStatefulAsync] Queued batch item #{index}: customId={customId}, tool={toolName}");
                        return new JObject { ["result"] = $"##SH_BATCH:{customId}##" };
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIStatefulAsync] BuildRequest failed for '{toolName}': {ex.Message}, falling back to sync");
                    }
                }
            }

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
                        ["messages"] = JArray.FromObject(toolResult.Messages),
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
                    ["messages"] = new JArray(new JObject
                    {
                        ["severity"] = "Error",
                        ["origin"] = "Return",
                        ["message"] = ex.Message
                    }),
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

            // Structured messages from toolResult are already surfaced via SurfaceMessagesFromReturn above
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
        protected AIReturn CurrentAIReturnSnapshot => this.AIReturnSnapshot;

        /// <summary>
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason).
        /// </summary>
        /// <param name="dA">The data access object.</param>
        protected void SetMetricsOutput(IGH_DataAccess dA)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            var metrics = this._persistedMetrics ?? this.AIReturnSnapshot?.Metrics;
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
                new JProperty("tokens_input_prompt", metrics.InputTokensPrompt),
                new JProperty("tokens_input_cached", metrics.InputTokensCached),
                new JProperty("tokens_input_cache_write", metrics.InputTokensCacheWrite),
                new JProperty("tokens_output", metrics.OutputTokens),
                new JProperty("tokens_output_reasoning", metrics.OutputTokensReasoning),
                new JProperty("tokens_output_generation", metrics.OutputTokensGeneration),
                new JProperty("finish_reason", metrics.FinishReason),
                new JProperty("completion_time", metrics.CompletionTime),
                new JProperty("context_usage_percent", metrics.ContextUsagePercent),
                new JProperty("data_count", this.DataCount),
                new JProperty("iterations_count", this.ProgressInfo.Total));

            // Convert metricsJson to GH_String
            var metricsJsonString = metricsJson.ToString();
            var ghString = new GH_String(metricsJsonString);

            // Set the metrics output
            this.SetPersistentOutput("Metrics", ghString, dA);

            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsJson}");
        }

        /// <summary>
        /// Lightweight read-only context passed to <see cref="PrepareInputs"/> and
        /// <see cref="TransformOutputs"/> for the current processing unit.
        /// </summary>
        protected readonly struct ProcessingUnitContext
        {
            /// <summary>Gets the data-tree path of the current processing unit.</summary>
            public GH_Path Path { get; init; }

            /// <summary>Gets the item index within the current branch, or null for branch-level processing.</summary>
            public int? ItemIndex { get; init; }

            /// <summary>Gets the sentinel custom ID for batch-mode units, or null in non-batch mode.</summary>
            public string SentinelId { get; init; }
        }

        /// <summary>
        /// Called before the AI tool is invoked for each processing unit.
        /// Override to transform, enrich, or validate inputs before they reach the AI pipeline.
        /// </summary>
        /// <param name="inputs">Mutable input dictionary for the current processing unit.</param>
        /// <param name="context">Read-only context (path, item index, topology) for the current unit.</param>
        protected virtual void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
        }

        /// <summary>
        /// Called after each AI result is decoded, before it is persisted.
        /// Override to split, reshape, or post-process outputs before they reach the canvas.
        /// </summary>
        /// <param name="decodedOutputs">Mutable output dictionary for the current processing unit.</param>
        /// <param name="context">Read-only context (path, item index, sentinel ID) for the current unit.</param>
        /// <returns>The (potentially modified) output dictionary.</returns>
        protected virtual Dictionary<string, IGH_Goo> TransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext context)
            => decodedOutputs;

        /// <summary>
        /// Combines additional metrics into <see cref="_persistedMetrics"/>.
        /// Use this from derived components that need to merge per-slot or per-item metrics
        /// that were not captured by <see cref="ProcessBatchResults{T}"/> (e.g. N→1 grouping).
        /// After calling this, invoke <see cref="SetMetricsOutput"/> to re-emit.
        /// </summary>
        /// <param name="metrics">The metrics to merge in.</param>
        protected void CombineIntoPersistedMetrics(AIMetrics metrics)
        {
            if (metrics == null) return;
            if (this._persistedMetrics == null)
            {
                this._persistedMetrics = new AIMetrics
                {
                    Provider = this.GetActualAIProviderName(),
                    Model = this.GetModel(),
                };
            }

            this._persistedMetrics.Combine(metrics);
        }

        /// <summary>
        /// Persists the primary output tree and any additional named outputs, then emits metrics.
        /// Call this from both the non-batch branch of <c>DoWorkAsync</c> and from
        /// <see cref="ProcessBatchResults{T}"/> to ensure a single finalization point.
        /// </summary>
        /// <typeparam name="T">The primary output Grasshopper goo type.</typeparam>
        /// <param name="primaryOutputParamName">Name of the primary output parameter.</param>
        /// <param name="primaryTree">The fully decoded primary output tree.</param>
        /// <param name="additionalOutputs">
        /// Zero or more (name, value) tuples for secondary outputs.
        /// Value routing: <see cref="IGH_Structure"/> → SetDataTree; <see cref="System.Collections.IEnumerable"/> (non-string) → SetDataList;
        /// anything else → SetData via <see cref="GH_Convert.ToGoo"/>.
        /// </param>
        protected void FinishResults<T>(
            string primaryOutputParamName,
            GH_Structure<T> primaryTree,
            params (string name, object value)[] additionalOutputs)
            where T : IGH_Goo
        {
            // Persist the primary output
            this.SetPersistentOutput(primaryOutputParamName, primaryTree, null);

            // Persist any additional outputs
            if (additionalOutputs != null)
            {
                foreach (var (name, value) in additionalOutputs)
                {
                    this.SetPersistentOutput(name, value, null);
                }
            }

            // Stamp CompletionTime into _persistedMetrics (the single authoritative metrics instance).
            // AIReturn.Metrics is computed fresh on every access — writing to it is a no-op.
            if (this._batchCompletionTime.HasValue)
            {
                if (this._persistedMetrics != null)
                {
                    this._persistedMetrics.CompletionTime = this._batchCompletionTime.Value;
                    Debug.WriteLine($"[AIStatefulAsync] FinishResults: stamped CompletionTime={this._batchCompletionTime.Value:F2}s into _persistedMetrics");
                }

                this._batchCompletionTime = null;
            }

            // Always emit metrics (replaces the ShouldEmitMetricsInPostSolve pattern)
            this.SetMetricsOutput(null);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // NOTE: AIReturnSnapshot clearing is done in OnEnteringNeedsRunState (on input change)
            // and defensively in OnStateProcessing, not on every solve.
            // This prevents metrics loss when batch completes and component re-enters Processing.
        }

        /// <inheritdoc/>
        protected override void OnEnteringNeedsRunState()
        {
            Debug.WriteLine("[AIStatefulAsyncComponentBase] Entering NeedsRun state - clearing previous run batch state");

            // Clear previous response metrics and all previous-run batch state.
            // _batchSubmission and _batchPollTimer are intentionally NOT cleared here:
            // an in-flight remote batch must keep polling until it completes or fails.
            this.AIReturnSnapshot = null;
            this._persistedMetrics = null;
            this._batchQueue = null;
            this._batchSentinelIds = null;
            this._batchProgressCompleted = 0;
            this._batchStartTime = null;
            this._batchCompletionTime = null;
            this._sentinelTrees = null;
            this.ResetProgress();
        }

        /// <inheritdoc/>
        protected override void OnEnteringProcessingState()
        {
            Debug.WriteLine("[AIStatefulAsyncComponentBase] Entering Processing state");

            // Defensively clear any queue/sentinel state that may have been accumulated
            // during the current Processing entry before a batch submission is made.
            // These are already null when arriving from NeedsRun; this guard covers
            // direct Processing transitions (e.g. RunOnlyOnInputChanges=false).
            this._batchQueue = null;
            this._batchSentinelIds = null;
            this._sentinelTrees = null;
        }

        protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
            // Skip output if batch is still active - outputs will be set after batch completes
            if (this._batchSubmission != null)
            {
                Debug.WriteLine("[AIStatefulAsync] OnSolveInstancePostSolve: Batch still active, skipping output");
                return;
            }

            // Metrics are emitted by FinishResults.
            // Components that set metrics synchronously in their own SolveInstance (e.g. AIChatComponent)
            // call SetMetricsOutput(DA) directly and do not go through FinishResults.

            // Badge cache was already updated in SolveInstance (before base.SolveInstance)
            // No need to update again here - the configured model hasn't changed
        }

        /// <summary>
        /// Overrides persistent output restoration to prevent sentinel trees from being output
        /// while a batch is actively being processed. Sentinel trees are internal implementation
        /// details used during batch submission and should not be visible on the canvas.
        /// </summary>
        protected override void RestorePersistentOutputs(IGH_DataAccess DA)
        {
            // If a batch is actively being processed, skip restoration of sentinel trees
            if (this._batchSubmission != null)
            {
                Debug.WriteLine("[AIStatefulAsync] RestorePersistentOutputs: Batch submission active, skipping sentinel tree output");
                return;
            }

            // Otherwise, restore outputs normally
            base.RestorePersistentOutputs(DA);
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

                Debug.WriteLine($"[UpdateBadgeCache] START: provider={providerName}, component={this.Name}");

                // Resolve model the user currently configured (for validation/replacement decisions)
                string configuredModel = this.GetModel();
                Debug.WriteLine($"[UpdateBadgeCache] configuredModel={configuredModel}, capability={this.RequiredCapability}");

                // If provider is missing, we cannot resolve anything
                if (string.IsNullOrWhiteSpace(providerName))
                {
                    this.badgeVerified = false;
                    this.badgeDeprecated = false;
                    this.badgeInvalidModel = true;
                    this.badgeReplacedModel = false;
                    this.badgeNotRecommended = false;
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
                Debug.WriteLine($"[UpdateBadgeCache] resolvedModel={resolvedModel}");

                // Gather validation messages (may include provider/model issues)
                var (isValid, validationMessages) = req.IsValid();
                Debug.WriteLine($"[UpdateBadgeCache] isValid={isValid}, messageCount={validationMessages?.Count ?? 0}");
                if (validationMessages != null)
                {
                    foreach (var msg in validationMessages)
                    {
                        Debug.WriteLine($"[UpdateBadgeCache]   - {msg.Severity} {msg.Code}: {msg.Message}");
                    }
                }

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
                // Do not mark as replaced if the provider has no capable model at all; that case must surface as Invalid.
                this.badgeReplacedModel = !hasNoCapableModel && (
                                           (!string.IsNullOrWhiteSpace(configuredModel)
                                            && !string.IsNullOrWhiteSpace(resolvedModel)
                                            && !string.Equals(configuredModel, resolvedModel, StringComparison.Ordinal))
                                           || hasCapabilityMismatch);
                Debug.WriteLine($"[UpdateBadgeCache] badgeReplacedModel={this.badgeReplacedModel}");

                // Invalid when missing/unknown provider, unknown model, no capable model, capability mismatch, or empty configured model
                this.badgeInvalidModel = string.IsNullOrWhiteSpace(configuredModel)
                                         || hasProviderMissing
                                         || hasUnknownProvider
                                         || hasNoCapableModel
                                         || hasCapabilityMismatch
                                         || this.badgeReplacedModel;
                Debug.WriteLine($"[UpdateBadgeCache] badgeInvalidModel={this.badgeInvalidModel}");

                // Read metadata from the resolved model to set Verified/Deprecated/NotRecommended when available
                var resolvedCaps = string.IsNullOrWhiteSpace(resolvedModel) ? null : ModelManager.Instance.GetCapabilities(providerName, resolvedModel);
                if (resolvedCaps == null)
                {
                    // No metadata available for the resolved model – do not render badges
                    this.badgeVerified = false;
                    this.badgeDeprecated = false;
                    this.badgeNotRecommended = false;
                    this.badgeCacheValid = true;
                }
                else
                {
                    // Verified/Deprecated reflect the model actually selected for execution; Verified requires capability match
                    this.badgeVerified = resolvedCaps.Verified && resolvedCaps.HasCapability(this.RequiredCapability);
                    this.badgeDeprecated = resolvedCaps.Deprecated;

                    // Check if model is discouraged for any of the AI tools used by this component
                    var toolNames = this.UsingAiTools;
                    this.badgeNotRecommended = toolNames != null && toolNames.Count > 0 &&
                                               resolvedCaps.IsDiscouragedForAnyTool(toolNames);
                    Debug.WriteLine($"[UpdateBadgeCache] notRecommended={this.badgeNotRecommended}, usingTools={string.Join(", ", toolNames ?? Array.Empty<string>())}");

                    this.badgeCacheValid = true;
                }

                Debug.WriteLine($"[UpdateBadgeCache] END: verified={this.badgeVerified}, deprecated={this.badgeDeprecated}, invalid={this.badgeInvalidModel}, replaced={this.badgeReplacedModel}, notRecommended={this.badgeNotRecommended}, cacheValid={this.badgeCacheValid}");

                return;
            }
            catch (Exception ex)
            {
                // On any failure, mark cache invalid to avoid rendering
                Debug.WriteLine($"[UpdateBadgeCache] EXCEPTION: {ex.Message}");
                this.badgeVerified = false;
                this.badgeDeprecated = false;
                this.badgeInvalidModel = false;
                this.badgeReplacedModel = false;
                this.badgeNotRecommended = false;
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
        /// <param name="notRecommended">True if the model is discouraged for the AI tools used by this component.</param>
        /// <returns>True if cache is valid; otherwise false.</returns>
        internal bool TryGetCachedBadgeFlags(out bool verified, out bool deprecated, out bool invalid, out bool replaced, out bool notRecommended)
        {
            verified = this.badgeVerified;
            deprecated = this.badgeDeprecated;
            invalid = this.badgeInvalidModel;
            replaced = this.badgeReplacedModel;
            notRecommended = this.badgeNotRecommended;
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

        #region PROGRESS

        /// <summary>
        /// Gets the current state message with progress information.
        /// During batch data-tree collection shows "Preparing X/X..."; while polling shows live "Processing batch (Y/X)...".
        /// </summary>
        /// <returns>A formatted state message string.</returns>
        public override string GetStateMessage()
        {
            // Don't show batch messages in terminal states - always use base message
            if (this.CurrentState != ComponentState.Processing)
            {
                return base.GetStateMessage();
            }

            // Batch submitted and polling: show live progress counter
            if (this._batchSubmission != null)
            {
                var total = this._batchSubmission.CustomIds?.Count ?? 0;
                return $"Processing batch ({this._batchProgressCompleted}/{total})...";
            }

            // Batch mode active but not yet submitted: data-tree is collecting items
            // Null-check ProgressInfo to prevent exceptions during component initialization
            if (this.IsBatchRequest() && this.ProgressInfo?.IsActive == true)
            {
                return $"Preparing {this.ProgressInfo.ProgressString}...";
            }

            return base.GetStateMessage();
        }

        #endregion

        #region BATCH

        /// <summary>
        /// Determines whether the current request parameters specify batch mode
        /// by checking the <see cref="AIRequestParameters.BatchTier"/> flag.
        /// </summary>
        protected bool IsBatchRequest() => this._requestParameters?.BatchTier == true;

        /// <summary>
        /// Stores the sentinel tree for a named output parameter so that
        /// <see cref="OnBatchCompleted"/> can reconstruct the output tree later.
        /// </summary>
        /// <typeparam name="T">The output goo type (typically <see cref="GH_String"/> for sentinel trees).</typeparam>
        /// <param name="paramName">Output parameter name (e.g., "Result").</param>
        /// <param name="tree">The sentinel tree produced during batch collection.</param>
        protected void StoreSentinelTree<T>(string paramName, GH_Structure<T> tree)
            where T : IGH_Goo
        {
            this._sentinelTrees ??= new Dictionary<string, object>();
            this._sentinelTrees[paramName] = tree;
        }

        /// <summary>
        /// Retrieves a previously stored sentinel tree for the given output parameter name.
        /// Returns <c>null</c> if no sentinel tree has been stored.
        /// </summary>
        /// <param name="paramName">Output parameter name (e.g., "Result").</param>
        /// <returns>The sentinel <see cref="GH_Structure{GH_String}"/>, or <c>null</c>.</returns>
        protected GH_Structure<GH_String> GetSentinelTree(string paramName)
        {
            if (this._sentinelTrees == null)
            {
                Debug.WriteLine($"[AIStatefulAsync] GetSentinelTree('{paramName}'): _sentinelTrees is null - tree was not persisted or restored");
                return null;
            }

            this._sentinelTrees.TryGetValue(paramName, out var tree);
            var result = tree as GH_Structure<GH_String>;
            Debug.WriteLine($"[AIStatefulAsync] GetSentinelTree('{paramName}'): {(result == null ? "NOT FOUND" : $"found, {result.PathCount} path(s), {result.DataCount} item(s)")}");
            return result;
        }

        /// <summary>
        /// Convenience helper for workers: after <c>RunProcessingAsync</c> finishes in batch mode,
        /// stores the sentinel tree for the given output parameter name and submits the batch queue.
        /// </summary>
        /// <typeparam name="T">The output goo type of the result dictionary.</typeparam>
        /// <param name="outputParamName">Output parameter name (e.g., "Result").</param>
        /// <param name="result">The result dictionary produced by <c>RunProcessingAsync</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <c>true</c> if batch was successfully submitted; <c>false</c> if not in batch mode
        /// or the provider does not support batch processing.
        /// </returns>
        protected async Task<bool> TrySubmitBatchAsync<T>(
            string outputParamName,
            Dictionary<string, GH_Structure<T>> result,
            CancellationToken ct)
            where T : IGH_Goo
        {
            if (!this.IsCurrentlyBatchMode()) return false;
            var submitted = await this.SubmitBatchQueueAsync(ct).ConfigureAwait(false);
            if (submitted && result != null && result.TryGetValue(outputParamName, out var sentinelTree))
            {
                this.StoreSentinelTree(outputParamName, sentinelTree);
            }

            return submitted;
        }

        /// <summary>Returns true when batch mode is active and items have been collected.</summary>
        private bool IsCurrentlyBatchMode() => this.IsBatchRequest() && this.HasBatchQueue;

        /// <summary>
        /// Gets a value indicating whether a batch job has been submitted and is currently being polled.
        /// Returns <c>false</c> during a fresh run (before submission) and after batch completion.
        /// Derived classes can use this to distinguish poll cycles from genuinely new runs.
        /// </summary>
        protected bool HasActiveBatchSubmission => this._batchSubmission != null;

        /// <summary>
        /// Gets a value indicating whether there are queued batch requests waiting to be submitted.
        /// </summary>
        protected bool HasBatchQueue => this._batchQueue?.Count > 0;

        /// <summary>
        /// Submits all queued batch requests as a single batch job via <see cref="IAIBatchProvider"/>
        /// and starts the poll timer. Call this from <c>DoWorkAsync</c> after
        /// <c>RunProcessingAsync</c> has finished collecting all items into the queue.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if submission succeeded; false if the provider doesn't support batch or the queue is empty.</returns>
        protected async Task<bool> SubmitBatchQueueAsync(CancellationToken cancellationToken = default)
        {
            // Prevent duplicate batch submissions
            if (this._batchSubmission != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] Batch submission already active ({this._batchSubmission.BatchId}), skipping duplicate submission");
                return false;
            }

            var queue = this._batchQueue;
            if (queue == null || queue.Count == 0) return false;
            this._batchQueue = null;

            var providerName = this.GetActualAIProviderName();
            var provider = ProviderManager.Instance.GetProvider(providerName);

            if (provider is not IAIBatchProvider batchProvider)
            {
                this.SetPersistentRuntimeMessage("batch_unsupported", GH_RuntimeMessageLevel.Warning,
                    $"Provider '{providerName}' does not support batch processing. Falling back to synchronous mode.", false);
                return false;
            }

            try
            {
                var submission = await batchProvider.SubmitBatchAsync(queue, cancellationToken).ConfigureAwait(false);
                this._batchSubmission = submission;
                this._batchProgressCompleted = 0;
                this._batchStartTime = DateTime.UtcNow;
                this.Message = $"Processing batch (0/{submission.CustomIds?.Count ?? 0})...";
                this.StartBatchPollTimer();
                Debug.WriteLine($"[AIStatefulAsync] Batch submitted: batchId={submission.BatchId}, itemCount={submission.CustomIds?.Count ?? 0}, startTime={this._batchStartTime:O}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Batch submit failed: {ex.Message}");
                this.SetPersistentRuntimeMessage("batch_error", GH_RuntimeMessageLevel.Error,
                    $"Batch submission failed: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// Starts (or restarts) the poll timer using the configured interval from <see cref="SmartHopperSettings.BatchPollIntervalMinutes"/>.
        /// </summary>
        private void StartBatchPollTimer()
        {
            StopBatchPollTimer();

            int intervalMs = Math.Max(1, SmartHopperSettings.Instance.BatchPollIntervalSeconds) * 1000;
            _batchPollTimer = new Timer(OnBatchPollTimerTick, null, intervalMs, intervalMs);
            Debug.WriteLine($"[AIStatefulAsync] Batch poll timer started, interval={intervalMs}ms, batchId={_batchSubmission?.BatchId}");
        }

        /// <summary>Stops and disposes the poll timer without cancelling the batch.</summary>
        private void StopBatchPollTimer()
        {
            var t = Interlocked.Exchange(ref _batchPollTimer, null);
            t?.Dispose();
        }

        /// <summary>Timer callback — fires on a thread-pool thread.</summary>
        private void OnBatchPollTimerTick(object state)
        {
            // Prevent overlapping polls
            if (Interlocked.CompareExchange(ref _batchPollRunning, 1, 0) != 0) return;

            try
            {
                _ = PollBatchStatusAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Poll tick error: {ex.Message}");
                Interlocked.Exchange(ref _batchPollRunning, 0);
            }
        }

        /// <summary>
        /// Asynchronously polls the provider for batch status and processes results when ready.
        /// </summary>
        private async Task PollBatchStatusAsync()
        {
            var submission = _batchSubmission;
            if (submission == null)
            {
                StopBatchPollTimer();
                Interlocked.Exchange(ref _batchPollRunning, 0);
                return;
            }

            try
            {
                var providerName = submission.ProviderName;
                var provider = ProviderManager.Instance.GetProvider(providerName);

                if (provider is not IAIBatchProvider batchProvider)
                {
                    StopBatchPollTimer();
                    return;
                }

                var status = await batchProvider.GetBatchStatusAsync(submission).ConfigureAwait(false);
                Debug.WriteLine($"[AIStatefulAsync] Batch poll result: state={status.State}, batchId={submission.BatchId}");

                switch (status.State)
                {
                    case AIBatchState.InProgress:
                        if (status.CompletedCount.HasValue)
                        {
                            _batchProgressCompleted = status.CompletedCount.Value;
                            var total = _batchSubmission?.CustomIds?.Count ?? 0;
                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                this.Message = $"Processing batch ({_batchProgressCompleted}/{total})...";
                                this.OnDisplayExpired(false);
                            });
                        }

                        break;

                    case AIBatchState.Completed:
                        StopBatchPollTimer();
                        _batchSubmission = null;

                        // Calculate batch completion time and store for FinishResults to consume
                        if (this._batchStartTime.HasValue)
                        {
                            this._batchCompletionTime = (DateTime.UtcNow - this._batchStartTime.Value).TotalSeconds;
                            Debug.WriteLine($"[AIStatefulAsync] Batch completed: batchId={submission.BatchId}, completionTime={this._batchCompletionTime:F2}s");
                            this._batchStartTime = null;
                        }

                        // Centralized batch finalization for success case
                        this.OnBatchFinalized(AIBatchState.Completed, status.Results, status.Messages, null);

                        // Transition to NeedsRun if sentinels remain unresolved (failed/missing results),
                        // otherwise transition to Completed. Detect unresolved sentinels by comparing
                        // sent sentinels (submission.CustomIds) with returned results keys.
                        var expectedCount = submission.CustomIds?.Count ?? 0;
                        var actualCount = status.Results?.Count ?? 0;
                        var hasUnresolvedSentinels = actualCount < expectedCount;

                        // Check for errors in messages to determine correct state
                        var hasErrors = status.Messages?.Any(m => m.Severity == AIRuntimeMessageSeverity.Error) ?? false;

                        if (hasErrors)
                        {
                            Debug.WriteLine($"[AIStatefulAsync] Batch completed but with errors, transitioning to Error");
                            this.StateManager.RequestTransition(ComponentState.Error, TransitionReason.Error);
                        }
                        else if (hasUnresolvedSentinels)
                        {
                            Debug.WriteLine($"[AIStatefulAsync] Batch completed but {expectedCount - actualCount}/{expectedCount} sentinels unresolved, transitioning to NeedsRun");
                            this.StateManager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged);
                        }
                        else
                        {
                            // Transition to Completed state now that batch is done
                            this.StateManager.RequestTransition(ComponentState.Completed, TransitionReason.ProcessingComplete);
                        }

                        Rhino.RhinoApp.InvokeOnUiThread(() => this.ExpireSolution(true));
                        break;

                    case AIBatchState.Failed:
                    case AIBatchState.Cancelled:
                    case AIBatchState.Expired:
                        StopBatchPollTimer();
                        _batchSubmission = null;

                        // Centralized batch finalization for terminal failure cases
                        this.OnBatchFinalized(status.State, null, status.Messages, status.ErrorMessage);

                        // Transition to Error state for terminal failures
                        this.StateManager.RequestTransition(ComponentState.Error, TransitionReason.Error);
                        Rhino.RhinoApp.InvokeOnUiThread(() => this.ExpireSolution(true));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] PollBatchStatus error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _batchPollRunning, 0);
            }
        }

        /// <summary>
        /// Called when a batch job completes. Override in derived classes to decode results,
        /// reconstruct output trees, and set persistent outputs.
        /// </summary>
        /// <param name="results">
        /// Dictionary mapping each <c>customId</c> (sentinel key) to the provider-specific
        /// raw response body. Use the provider's <c>Decode</c> method on each value to extract
        /// the AI response.
        /// </param>
        /// <param name="messages">
        /// Item-level diagnostic messages from the provider (errors, warnings, info).
        /// May be null or empty if all items succeeded.
        /// </param>
        protected virtual void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<AIRuntimeMessage> messages = null)
        {
        }

        /// <summary>
        /// Centralized batch finalization handler called for both successful completion
        /// and terminal failure states (Failed, Cancelled, Expired). Surfaces all messages
        /// via <see cref="SurfaceMessagesFromReturn"/> and delegates to <see cref="OnBatchCompleted"/>
        /// for success cases.
        /// </summary>
        /// <param name="state">The final batch state.</param>
        /// <param name="results">Result dictionary for success cases; null for failures.</param>
        /// <param name="messages">Item-level diagnostic messages from the provider.</param>
        /// <param name="errorMessage">Error description for failure states.</param>
        private void OnBatchFinalized(AIBatchState state, IReadOnlyDictionary<string, JObject> results, IReadOnlyList<AIRuntimeMessage> messages, string errorMessage)
        {
            // Build a unified AIReturn to collect all messages for surfacing
            var unifiedReturn = new AIReturn();
            var hasMessages = false;

            // Add terminal state message for failures
            if (state != AIBatchState.Completed)
            {
                unifiedReturn.AddRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Provider,
                    $"Batch {state.ToString().ToLowerInvariant()}: {errorMessage ?? "no details"}");
                hasMessages = true;
            }

            // Add all item-level messages (works for both success and failure)
            if (messages != null && messages.Count > 0)
            {
                foreach (var msg in messages)
                {
                    unifiedReturn.AddRuntimeMessage(msg.Severity, msg.Origin, msg.Message);
                }

                hasMessages = true;
            }

            // Surface all collected messages in one call
            if (hasMessages)
            {
                this.SurfaceMessagesFromReturn(unifiedReturn, "batch_item");
            }

            // For success states, delegate to the virtual OnBatchCompleted hook
            if (state == AIBatchState.Completed && (results != null || hasMessages))
            {
                try
                {
                    this.OnBatchCompleted(results, messages);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIStatefulAsync] OnBatchCompleted error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Helper method to process batch results: reconstructs output tree by replacing sentinels,
        /// calls <see cref="TransformOutputs"/> on each decoded item to allow post-processing,
        /// aggregates metrics from all batch items, surfaces any batch errors via <see cref="AIReturn"/>,
        /// and delegates to <see cref="FinishResults{T}"/> to persist all outputs atomically and emit metrics.
        /// Call this from <see cref="OnBatchCompleted"/> to handle common batch completion logic.
        /// </summary>
        /// <typeparam name="T">The output Grasshopper goo type.</typeparam>
        /// <param name="outputParamName">Name of the primary output parameter to persist (e.g., "Result").</param>
        /// <param name="sentinelTree">Tree containing sentinel strings from batch submission.</param>
        /// <param name="results">Dictionary from customId to provider response body.</param>
        /// <param name="decode">Function that converts (customId, resultBody) into a goo item.</param>
        /// <param name="messages">Optional item-level diagnostic messages (errors/warnings) from the provider.</param>
        /// <returns>The reconstructed output tree with sentinels replaced by decoded values.</returns>
        protected GH_Structure<T> ProcessBatchResults<T>(
            string outputParamName,
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, JObject> results,
            Func<string, JObject, T> decode,
            IReadOnlyList<AIRuntimeMessage> messages = null)
            where T : IGH_Goo
        {
            if (results == null || sentinelTree == null)
            {
                // Still finalize with an empty tree to clear any previous sentinels and emit metrics
                var emptyTree = new GH_Structure<T>();
                this.FinishResults(outputParamName, emptyTree);
                return emptyTree;
            }

            var providerName = this.GetActualAIProviderName();
            var provider = ProviderManager.Instance.GetProvider(providerName);
            if (provider == null)
            {
                var emptyTree = new GH_Structure<T>();
                this.FinishResults(outputParamName, emptyTree);
                return emptyTree;
            }

            var allInteractions = new List<IAIInteraction>();
            var allMetrics = new List<AIMetrics>();

            // Accumulate extra outputs returned by TransformOutputs across all sentinels.
            // Key: output param name → merged GH_Structure<IGH_Goo> (one slot per sentinel path).
            var extraOutputAccumulator = new Dictionary<string, GH_Structure<IGH_Goo>>();

            var reconstructedTree = ReconstructOutputTree<T>(
                sentinelTree,
                results,
                (customId, resultBody) =>
                {
                    try
                    {
                        var interactions = provider.Decode(resultBody);
                        if (interactions != null)
                        {
                            allInteractions.AddRange(interactions);

                            // Extract metrics from each interaction
                            foreach (var interaction in interactions)
                            {
                                if (interaction.Metrics != null)
                                {
                                    allMetrics.Add(interaction.Metrics);
                                }
                            }
                        }

                        var primaryItem = decode(customId, resultBody);

                        // Call TransformOutputs so derived components can reshape/split results
                        var context = new ProcessingUnitContext { SentinelId = customId };
                        var decodedMap = new Dictionary<string, IGH_Goo> { [outputParamName] = primaryItem };
                        var transformed = this.TransformOutputs(decodedMap, context);

                        // Accumulate any extra keys returned by TransformOutputs
                        if (transformed != null)
                        {
                            foreach (var kvp in transformed)
                            {
                                if (kvp.Key == outputParamName) continue;
                                if (!extraOutputAccumulator.TryGetValue(kvp.Key, out var extraTree))
                                {
                                    extraTree = new GH_Structure<IGH_Goo>();
                                    extraOutputAccumulator[kvp.Key] = extraTree;
                                }

                                extraTree.Append(kvp.Value);
                            }
                        }

                        return primaryItem;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIStatefulAsync] Batch decode error for {customId}: {ex.Message}");
                        return decode(customId, resultBody);
                    }
                });

            // Surface any provider messages (errors/warnings) from individual batch items.
            // This mirrors how AIRequestCall.Exec() surfaces errors via SurfaceMessagesFromReturn.
            var errorInteractions = allInteractions.OfType<AIInteractionError>().ToList();
            if (errorInteractions.Count > 0 || (messages != null && messages.Count > 0))
            {
                var errorReturn = new AIReturn();
                foreach (var err in errorInteractions)
                {
                    errorReturn.AddRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Provider,
                        err.Content ?? "Provider returned an error");
                }

                // Surface item-level provider messages (errors, warnings, info)
                if (messages != null)
                {
                    foreach (var msg in messages)
                    {
                        errorReturn.AddRuntimeMessage(msg.Severity, msg.Origin, msg.Message);
                    }
                }

                this.SurfaceMessagesFromReturn(errorReturn, "batch_item");
            }

            // Build aggregated AIReturn so body/interactions are available after batch completion.
            // Also build _persistedMetrics as the single authoritative metrics instance:
            // AIReturn.Metrics is computed fresh on every access, so any mutation to it is a no-op.
            if (allInteractions.Count > 0)
            {
                // Aggregate all per-interaction metrics into one named instance
                var aggregatedMetrics = new AIMetrics
                {
                    Provider = this.GetActualAIProviderName(),
                    Model = this.GetModel(),
                };
                foreach (var m in allMetrics)
                {
                    aggregatedMetrics.Combine(m);
                }

                Debug.WriteLine($"[AIStatefulAsync] Aggregated batch metrics: {allMetrics.Count} items, " +
                              $"InputTokens={aggregatedMetrics.InputTokens}, OutputTokens={aggregatedMetrics.OutputTokens}");

                // Store as the single authoritative source for SetMetricsOutput
                this._persistedMetrics = aggregatedMetrics;

                // Build AIReturn for body/interactions (used by CurrentAIReturnSnapshot consumers)
                var batchReturn = new AIReturn();
                var batchRequest = new AIRequestCall();
                batchRequest.Initialize(
                    aggregatedMetrics.Provider,
                    aggregatedMetrics.Model,
                    new List<IAIInteraction>(),
                    endpoint: "batch_complete",
                    capability: AICapability.None,
                    toolFilter: null);
                batchReturn.CreateSuccess(allInteractions, request: batchRequest);
                this.SetAIReturnSnapshot(batchReturn);
            }

            // Build additionalOutputs array for FinishResults
            var additionalOutputs = extraOutputAccumulator
                .Select(kvp => (kvp.Key, (object)kvp.Value))
                .ToArray();

            // Delegate to FinishResults: persists primary + extras + emits metrics atomically
            this.FinishResults(outputParamName, reconstructedTree, additionalOutputs);

            return reconstructedTree;
        }

        /// <summary>
        /// Reconstructs a Grasshopper data tree by replacing sentinel placeholder strings
        /// (format: <c>##SH_BATCH:{customId}##</c>) with decoded values.
        /// Paths and non-sentinel items are preserved unchanged.
        /// </summary>
        /// <typeparam name="T">The output Grasshopper goo type.</typeparam>
        /// <param name="sentinelTree">Tree containing sentinel strings and normal items.</param>
        /// <param name="results">Dictionary from customId to provider response body.</param>
        /// <param name="decode">Function that converts (customId, resultBody) into a goo item.</param>
        /// <returns>New tree with sentinels replaced by decoded values.</returns>
        protected static GH_Structure<T> ReconstructOutputTree<T>(
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, JObject> results,
            Func<string, JObject, T> decode)
            where T : IGH_Goo
        {
            const string prefix = "##SH_BATCH:";
            const string suffix = "##";
            var newTree = new GH_Structure<T>();
            if (sentinelTree == null) return newTree;

            foreach (var path in sentinelTree.Paths)
            {
                var branch = sentinelTree.get_Branch(path);
                var newBranch = new List<T>();
                foreach (GH_String item in branch)
                {
                    var str = item?.Value ?? string.Empty;
                    if (str.StartsWith(prefix, StringComparison.Ordinal) &&
                        str.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        var customId = str.Substring(prefix.Length, str.Length - prefix.Length - suffix.Length);
                        if (results != null && results.TryGetValue(customId, out var resultBody))
                        {
                            newBranch.Add(decode(customId, resultBody));
                            continue;
                        }
                    }

                    if (item is T t) newBranch.Add(t);
                }

                newTree.AppendRange(newBranch, path);
            }

            return newTree;
        }

        /// <inheritdoc/>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;

            try
            {
                // Persist batch state so polling can resume after file close/reopen
                if (_batchSubmission != null)
                {
                    writer.SetString("BatchId", _batchSubmission.BatchId);
                    writer.SetString("BatchProvider", _batchSubmission.ProviderName);
                    writer.SetString("BatchRequest", _batchSubmission.SerializedRequest ?? string.Empty);
                    writer.SetString("BatchSubmittedAt", _batchSubmission.SubmittedAt.ToString("O"));

                    if (_batchSubmission.CustomIds != null && _batchSubmission.CustomIds.Count > 0)
                    {
                        writer.SetString("BatchCustomIds", new JArray(_batchSubmission.CustomIds.ToArray()).ToString(Newtonsoft.Json.Formatting.None));
                    }

                    Debug.WriteLine($"[AIStatefulAsync] Write: persisted batch state, batchId={_batchSubmission.BatchId}, items={_batchSubmission.CustomIds?.Count ?? 0}");
                }

                // Persist sentinel IDs so OnBatchCompleted can reconstruct trees after reload
                if (_batchSentinelIds != null && _batchSentinelIds.Count > 0)
                {
                    writer.SetString("BatchSentinelIds", new JArray(_batchSentinelIds.ToArray()).ToString(Newtonsoft.Json.Formatting.None));
                }

                // Persist sentinel trees (path layout + sentinel strings) so OnBatchCompleted
                // can reconstruct output trees correctly after file close/reopen.
                // Without this, GetSentinelTree() returns null on reload and no output is produced.
                if (_sentinelTrees != null && _sentinelTrees.Count > 0)
                {
                    var sentinelTreesJson = new JObject();
                    foreach (var kvp in _sentinelTrees)
                    {
                        if (kvp.Value is GH_Structure<GH_String> tree)
                        {
                            var treeJson = new JArray();
                            foreach (var path in tree.Paths)
                            {
                                var pathIndices = new JArray(path.Indices.Cast<object>().ToArray());
                                var items = tree.get_Branch(path);
                                var itemsJson = new JArray();
                                foreach (GH_String item in items)
                                {
                                    itemsJson.Add(item?.Value ?? string.Empty);
                                }

                                treeJson.Add(new JObject
                                {
                                    ["path"] = pathIndices,
                                    ["items"] = itemsJson,
                                });
                            }

                            sentinelTreesJson[kvp.Key] = treeJson;
                        }
                    }

                    writer.SetString("BatchSentinelTrees", sentinelTreesJson.ToString(Newtonsoft.Json.Formatting.None));
                    Debug.WriteLine($"[AIStatefulAsync] Write: persisted {_sentinelTrees.Count} sentinel tree(s)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Write batch state error: {ex.Message}");
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader)) return false;

            try
            {
                if (reader.ItemExists("BatchId"))
                {
                    var batchId = reader.GetString("BatchId");
                    var providerName = reader.ItemExists("BatchProvider") ? reader.GetString("BatchProvider") : string.Empty;
                    var serializedReq = reader.ItemExists("BatchRequest") ? reader.GetString("BatchRequest") : string.Empty;

                    IReadOnlyList<string> customIds = null;
                    if (reader.ItemExists("BatchCustomIds"))
                    {
                        var idsJson = reader.GetString("BatchCustomIds");
                        if (!string.IsNullOrEmpty(idsJson))
                        {
                            customIds = JArray.Parse(idsJson).Values<string>().ToList().AsReadOnly();
                        }
                    }
                    else if (reader.ItemExists("BatchCustomId"))
                    {
                        // Legacy single-ID format
                        var singleId = reader.GetString("BatchCustomId");
                        if (!string.IsNullOrEmpty(singleId))
                            customIds = new List<string> { singleId }.AsReadOnly();
                    }

                    if (!string.IsNullOrEmpty(batchId) && !string.IsNullOrEmpty(providerName))
                    {
                        _batchSubmission = new AIBatchSubmission(batchId, providerName, serializedReq,
                            customIds ?? new List<string>().AsReadOnly());
                        Debug.WriteLine($"[AIStatefulAsync] Read: restored batch state, batchId={batchId}, items={customIds?.Count ?? 0}");

                        // Restore component to Processing state so sentinel values aren't output
                        this.StateManager.ForceState(ComponentState.Processing);
                        Debug.WriteLine($"[AIStatefulAsync] Read: restored state to Processing for active batch");

                        // Resume polling — defer until after component is fully loaded
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            if (_batchSubmission != null)
                            {
                                StartBatchPollTimer();

                                // Expire solution to trigger recompute with Processing state
                                this.ExpireSolution(true);
                            }
                        });
                    }
                }

                if (reader.ItemExists("BatchSentinelIds"))
                {
                    var sentinelJson = reader.GetString("BatchSentinelIds");
                    if (!string.IsNullOrEmpty(sentinelJson))
                    {
                        _batchSentinelIds = new HashSet<string>(JArray.Parse(sentinelJson).Values<string>());
                        Debug.WriteLine($"[AIStatefulAsync] Read: restored {_batchSentinelIds.Count} sentinel IDs");
                    }
                }

                if (reader.ItemExists("BatchSentinelTrees"))
                {
                    var treesJson = reader.GetString("BatchSentinelTrees");
                    if (!string.IsNullOrEmpty(treesJson))
                    {
                        var treesObj = JObject.Parse(treesJson);
                        _sentinelTrees = new Dictionary<string, object>();
                        foreach (var prop in treesObj.Properties())
                        {
                            var tree = new GH_Structure<GH_String>();
                            foreach (var branchToken in prop.Value as JArray ?? new JArray())
                            {
                                var pathIndices = (branchToken["path"] as JArray)?.Values<int>().ToArray() ?? Array.Empty<int>();
                                var ghPath = new Grasshopper.Kernel.Data.GH_Path(pathIndices);
                                var items = (branchToken["items"] as JArray) ?? new JArray();
                                foreach (var itemToken in items)
                                {
                                    tree.Append(new GH_String(itemToken.ToString()), ghPath);
                                }
                            }

                            _sentinelTrees[prop.Name] = tree;
                        }

                        Debug.WriteLine($"[AIStatefulAsync] Read: restored {_sentinelTrees.Count} sentinel tree(s)");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Read batch state error: {ex.Message}");
            }

            return true;
        }

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            // Stop batch polling when component is removed from document (file closed)
            // Polling will resume when file is reopened via Read() method
            if (this._batchPollTimer != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] RemovedFromDocument: Stopping batch poll timer");
                this.StopBatchPollTimer();
            }

            base.RemovedFromDocument(document);
        }

        /// <inheritdoc/>
        protected override void OnWorkerCompleted()
        {
            // If a batch is active, don't transition to Completed yet.
            // The batch polling will handle the transition when results are ready.
            if (this._batchSubmission != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] OnWorkerCompleted: Batch submission active ({this._batchSubmission.BatchId}), staying in Processing state");

                // Stay in Processing state - batch polling will transition to Completed
                // Still commit hashes so we don't re-trigger processing
                this.StateManager.CommitHashes();
                this.StateManager.CancelDebounce();

                // Don't call base.OnWorkerCompleted() which would transition to Completed
                // Don't expire solution - the batch poll will do it when complete
                return;
            }

            // No active batch - proceed with normal completion
            base.OnWorkerCompleted();
        }

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

        #endregion
    }
}
