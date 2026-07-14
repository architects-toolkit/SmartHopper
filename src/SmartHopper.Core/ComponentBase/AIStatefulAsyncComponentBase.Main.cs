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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Diagnostics;

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
    /// <see cref="PrepareInputs"/> and <see cref="SentinelTransformOutputs"/> fire at identical logical
    /// positions in both paths.
    /// </para>
    /// <para>
    /// For output adapter components that need full provider chat completion with forced tool calling,
    /// use <see cref="CallAIAsync"/> instead of <see cref="CallAIToolAsync"/>. This method supports
    /// conversation context and forced tool execution for components like AI2Text, AI2Number, etc.
    /// </para>
    ///
    /// <para><b>NON-BATCH PATH:</b></para>
    /// <code>
    /// DoWorkAsync()
    ///   └── RunProcessingAsync()
    ///         └── DataTreeProcessor.RunAsync()
    ///               └── function(inputs)          [called once per branch/item]
    ///                     1. PrepareInputs(inputs, context)    ← virtual hook
    ///                     2. CallAIToolAsync(...)
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
    ///                     2. CallAIToolAsync(...)
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
    ///               SentinelTransformOutputs({primary→item}, context)  ← virtual hook
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
    public abstract partial class AIStatefulAsyncComponentBase : AIProviderComponentBase
    {
        /// <summary>
        /// Per-request AI parameters (model, temperature, max tokens, extras) from the Settings input.
        /// </summary>
        private AIRequestParameters _requestParameters;

        /// <summary>
        /// Bundles the nine batch / metrics fields that previously lived directly on
        /// the base class (submission, sentinel trees, queue, sentinel ids, progress,
        /// unsupported-checked flag, start/completion time, persisted metrics).
        /// See <see cref="BatchRunState"/> for the per-run vs. cross-run lifecycle
        /// contract — this object centralises the reset semantics so
        /// <see cref="OnEnteringNeedsRun"/> shrinks to a single call.
        /// </summary>
        private readonly BatchRunState _batchState = new BatchRunState();

        /// <summary>
        /// When true, <see cref="SubmitBatchQueueAsync"/> collects the batch queue and sentinel
        /// trees from the component's normal processing, but does NOT submit to the provider.
        /// Instead, it feeds <see cref="_pendingFileLoadedResults"/> through the normal completion
        /// pipeline, mapping results to collected custom IDs in order.
        /// Set by <see cref="LoadResultsFromFile"/> when sentinel trees are missing.
        /// Cleared after fallback finalization completes.
        /// </summary>
        private bool _batchCollectOnly;

        /// <summary>
        /// File-loaded batch results staged for order-based fallback mapping.
        /// Populated by <see cref="LoadResultsFromFile"/> before triggering the collect-only run.
        /// Consumed during the collect-only submission path.
        /// </summary>
        private IReadOnlyDictionary<string, JObject> _pendingFileLoadedResults;

        /// <summary>Messages staged alongside <see cref="_pendingFileLoadedResults"/>.</summary>
        private List<SHRuntimeMessage> _pendingFileLoadMessages;

        /// <summary>Timer that fires batch status polls.</summary>
        private System.Threading.Timer _batchPollTimer;

        /// <summary>Guards against concurrent poll calls.</summary>
        private int _batchPollRunning;

        /// <summary>Maximum time to poll for batch completion (24 hours + 60 seconds to match typical provider limits + extra 60 seconds buffer).</summary>
        private static readonly TimeSpan MaxBatchPollingDuration = TimeSpan.FromHours(24) + TimeSpan.FromSeconds(60);

        /// <summary>
        /// Last AI return snapshot stored by this component.
        /// </summary>
        private AIReturn AIReturnSnapshot;

        /// <summary>
        /// Path of the processing unit currently being executed (set by
        /// <see cref="OnProcessingUnitStart"/>). Null outside of active processing.
        /// </summary>
        private GH_Path _currentProcessingPath;

        /// <summary>
        /// Item index of the processing unit currently being executed (set by
        /// <see cref="OnProcessingUnitStart"/>). Null for branch-level topologies.
        /// </summary>
        private int? _currentProcessingItemIndex;

        /// <summary>
        /// Metrics tree built during the current solve. Each metric JSON string is placed
        /// at the same path as the branch/item that produced it, so downstream components
        /// can deconstruct metrics while preserving tree topology.
        /// </summary>
        private GH_Structure<GH_String> _metricsTree;

        /// <summary>
        /// Gets or sets the authoritative metrics list for multi-provider support.
        /// Use <see cref="CombineIntoPersistedMetrics(AIMetrics, string)"/> to add entries.
        /// </summary>
        protected Infrastructure.AICall.Metrics.AIMetricsList PersistedMetricsList
        {
            get => this._batchState.PersistedMetricsList;
            set => this._batchState.PersistedMetricsList = value;
        }

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

            pManager.AddGenericParameter(WellKnownInputs.Settings, "S", "AI request settings (model, temperature, tokens, extras).\nConnect an AI Settings component or enter a model name as text for quick setup.\nLeave empty to use all provider defaults.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddBooleanParameter(WellKnownInputs.Run, "R", "Set this parameter to true to run the component.", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers output parameters for the component.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Allow derived classes to add their specific outputs
            base.RegisterOutputParams(pManager);

            pManager.AddTextParameter(WellKnownInputs.Metrics, "M", "Usage metrics in JSON format including input tokens, output tokens, and completion time. Preserves branch topology — one metric item per output branch.", GH_ParamAccess.tree);
        }

        #endregion
    }
}
