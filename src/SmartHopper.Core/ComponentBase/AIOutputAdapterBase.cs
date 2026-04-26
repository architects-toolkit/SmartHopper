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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Validation;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Defines how to extract a single output from an AIReturn.
    /// </summary>
    public class OutputMapping
    {
        /// <summary>
        /// Gets or sets the Grasshopper parameter name for this output.
        /// </summary>
        public string ParamName { get; set; }

        /// <summary>
        /// Gets or sets the nickname for this output parameter.
        /// If null or empty, defaults to the first character of ParamName.
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// Gets or sets the description for this output parameter.
        /// If null or empty, defaults to "Output: {ParamName}".
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the access mode for this output parameter.
        /// Defaults to GH_ParamAccess.tree.
        /// </summary>
        public GH_ParamAccess Access { get; set; } = GH_ParamAccess.tree;

        /// <summary>
        /// Gets or sets the Grasshopper parameter type for this output.
        /// Used to register the output with the correct type (e.g., Param_String, Param_Number).
        /// If null, defaults to generic parameter.
        /// </summary>
        public Type ParamType { get; set; }

        /// <summary>
        /// Gets or sets the function that extracts this output from an AIReturn.
        /// </summary>
        public Func<AIReturn, IGH_Goo> Extractor { get; set; }
    }

    /// <summary>
    /// Base class for AI output adapter components that receive AIInputPayload inputs,
    /// perform AI processing via data tree branch-by-branch, and produce typed outputs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Output adapters follow the unified execution flow of AIStatefulAsyncComponentBase:
    /// - Non-batch: inputs → PrepareInputs → CallAIToolAsync → SentinelTransformOutputs → FinishResults
    /// - Batch: inputs → PrepareInputs → CallAIToolAsync (queued) → OnBatchCompleted → ProcessBatchResults → FinishResults
    /// </para>
    /// <para>
    /// Both paths automatically handle multiple outputs via <see cref="GetOutputMappings"/>.
    /// Capability requirements are derived from <see cref="UsingAiTools"/> (tool capabilities are merged automatically).
    /// Subclasses must:
    /// 1. Override <see cref="UsingAiTools"/> to declare which AI tools this component uses (optional)
    /// 2. Override <see cref="GetInternalSystemPrompt"/> to define the task contract
    /// 3. Override <see cref="GetOutputMappings"/> to define output extraction logic
    /// 4. Override <see cref="RegisterAdditionalInputParams"/> and <see cref="RegisterAdditionalOutputParams"/> for custom parameters
    /// </para>
    /// <para>
    /// The base class automatically:
    /// - Applies all output mappings in <see cref="SentinelTransformOutputs"/> (non-batch)
    /// - Applies all output mappings in <see cref="OnBatchCompleted"/> (batch)
    /// - Calls <see cref="FinishResults"/> with all outputs atomically
    /// - Merges tool capabilities from <see cref="UsingAiTools"/> into effective capability
    /// </para>
    /// </remarks>
    public abstract class AIOutputAdapterBase : AIStatefulAsyncComponentBase
    {
        private readonly GH_Exposure _exposure;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIOutputAdapterBase"/> class.
        /// </summary>
        /// <param name="name">Component name.</param>
        /// <param name="nickname">Component nickname.</param>
        /// <param name="description">Component description.</param>
        /// <param name="exposure">Component exposure level (primary or secondary).</param>
        protected AIOutputAdapterBase(string name, string nickname, string description, GH_Exposure exposure)
            : base(name, nickname, description, "SmartHopper", "Output")
        {
            this._exposure = exposure;
        }

        /// <summary>
        /// Gets the component exposure level (primary or secondary).
        /// </summary>
        public override GH_Exposure Exposure => this._exposure;

        /// <summary>
        /// Gets the internal system prompt that defines this component's task contract.
        /// This is always placed first in the final system message.
        /// </summary>
        /// <returns>The internal system prompt string.</returns>
        protected abstract string GetInternalSystemPrompt();

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected abstract Bitmap Icon { get; }

        /// <summary>
        /// Gets the output mappings that define how to extract results from an AIReturn.
        /// The first mapping is treated as the primary output for batch processing.
        /// </summary>
        /// <returns>A read-only list of output mappings. Must not be empty.</returns>
        protected abstract IReadOnlyList<OutputMapping> GetOutputMappings();

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload(s) containing the input data for AI processing.", GH_ParamAccess.tree);

            base.RegisterInputParams(pManager);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Define extra input parameters if needed
        }

        /// <summary>
        /// Gathers additional input parameters beyond "Input >" at gather-time.
        /// Override in subclasses that declare extra parameters via <see cref="RegisterAdditionalInputParams"/>.
        /// Values stored here are injected into the <c>inputs</c> dictionary before <see cref="PrepareInputs"/> is called.
        /// </summary>
        /// <param name="DA">Data access object for reading inputs.</param>
        /// <param name="additionalInputs">Dictionary to populate. Keys must match parameter names used in <see cref="PrepareInputs"/>.</param>
        protected virtual void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Output adapters process each branch as a single conversation unit:
        /// all payloads within a branch are merged into one AIBody → one API call.
        /// BranchToBranch ensures <c>ProcessBranchAsync</c> receives the full payload list per branch.
        /// </remarks>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = false,
        };

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Preferible use GetOutputMappings() to define output parameters
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            var mappings = this.GetOutputMappings();
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    var nickname = !string.IsNullOrWhiteSpace(mapping.NickName)
                        ? mapping.NickName
                        : (mapping.ParamName.Length > 0 ? mapping.ParamName.Substring(0, 1) : "O");
                    var description = !string.IsNullOrWhiteSpace(mapping.Description)
                        ? mapping.Description
                        : $"Output: {mapping.ParamName}";

                    if (mapping.ParamType != null)
                    {
                        var param = (IGH_Param)Activator.CreateInstance(mapping.ParamType);
                        param.Name = mapping.ParamName;
                        param.NickName = nickname;
                        param.Description = description;
                        param.Access = mapping.Access;
                        pManager.AddParameter(param);
                    }
                    else
                    {
                        pManager.AddGenericParameter(mapping.ParamName, nickname, description, mapping.Access);
                    }
                }
            }

            base.RegisterOutputParams(pManager);
        }

        /// <inheritdoc/>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            base.PrepareInputs(inputs, context);

            // Validate Simple vs Complex case: >1 UsingAiTools requires override
            var usingAiTools = this.UsingAiTools;
            if (usingAiTools != null && usingAiTools.Count > 1)
            {
                throw new InvalidOperationException(
                    "Components with >1 UsingAiTools must override PrepareInputs (Complex Case).");
            }

            // Validate capabilities before AI call (RequiredCapability automatically merges UsingAiTools)
            var validation = new ComponentCapabilityValidator(this.GetActualAIProviderName(), this.GetModel())
                .ValidateSync(this.RequiredCapability);

            if (!validation.IsValid)
            {
                var errorMsg = validation.Messages?.FirstOrDefault(m => m.Severity == SHRuntimeMessageSeverity.Error);
                throw new InvalidOperationException(
                    $"[Capability] {errorMsg?.Message ?? "Provider/model does not support required capability"}");
            }

            // Merge AIInputPayload inputs per-branch and build system prompt
            if (inputs.TryGetValue("Input >", out var payloadTreeObj) && payloadTreeObj is GH_Structure<GH_AIInputPayload> payloadTree)
            {
                var mergedBodies = AIInputPayloadMerger.MergePayloadsPerBranch(payloadTree);

                // For this branch, get the merged body
                if (mergedBodies.TryGetValue(context.Path, out var mergedBody))
                {
                    // Build final system prompt
                    var builder = new SystemPromptBuilder()
                        .WithInternalPrompt(this.GetInternalSystemPrompt());

                    // Extract context filter from merged body if present
                    if (mergedBody != null && !string.IsNullOrWhiteSpace(mergedBody.ContextFilter) && mergedBody.ContextFilter != "-*")
                    {
                        builder.WithContext(mergedBody.ContextFilter.Split(','));
                    }

                    // Extract user instructions from System agent interactions in merged body
                    if (mergedBody?.Interactions != null)
                    {
                        var systemInteractions = mergedBody.Interactions
                            .Where(i => i.Agent == AIAgent.System)
                            .OfType<AIInteractionText>()
                            .ToList();

                        if (systemInteractions.Count > 0)
                        {
                            builder.WithUserInstructions(systemInteractions);
                        }
                    }

                    var systemPrompt = builder.Build();

                    // Merge with the payload body (system prompt first, then interactions)
                    var combinedBody = AIBodyBuilder.Create()
                        .AddText(AIAgent.System, systemPrompt);

                    if (mergedBody != null && mergedBody.Interactions != null)
                    {
                        foreach (var interaction in mergedBody.Interactions)
                        {
                            combinedBody.Add(interaction);
                        }
                    }

                    // Add tool filter for forced tool call
                    var toolFilter = mergedBody?.ToolFilter ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(toolFilter))
                    {
                        combinedBody.WithToolFilter(toolFilter);
                    }

                    inputs["_MergedBody"] = combinedBody.Build();
                }
            }
        }

        /// <inheritdoc/>
        protected override Dictionary<string, IGH_Goo> SentinelTransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext context)
        {
            base.SentinelTransformOutputs(decodedOutputs, context);

            if (this.CurrentAIReturnSnapshot == null)
                return decodedOutputs;

            var mappings = this.GetOutputMappings();
            if (mappings == null || mappings.Count == 0)
                return decodedOutputs;

            foreach (var mapping in mappings)
            {
                var result = mapping.Extractor(this.CurrentAIReturnSnapshot);
                if (result != null)
                {
                    decodedOutputs[mapping.ParamName] = result;
                }
            }

            return decodedOutputs;
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(
            IReadOnlyDictionary<string, JObject> results,
            IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var mappings = this.GetOutputMappings();
            if (mappings == null || mappings.Count == 0 || results == null)
                return;

            var primaryMapping = mappings[0];
            var sentinel = this.GetSentinelTree(primaryMapping.ParamName);
            if (sentinel == null)
                return;

            this.ProcessBatchResults<IGH_Goo>(
                primaryMapping.ParamName,
                sentinel,
                results,
                (customId, body) =>
                {
                    // Per-item decode wrapper; the real request/metrics are aggregated by
                    // ProcessBatchResults at the batch level, not per item.
                    var aiReturn = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    aiReturn.SetBody(body);
                    return primaryMapping.Extractor(aiReturn);
                },
                messages);
        }

        /// <summary>
        /// Provides the built-in worker that drives the output adapter pipeline.
        /// Subclasses must NOT override this; override <see cref="PrepareInputs"/>,
        /// <see cref="SentinelTransformOutputs"/>, and <see cref="GetOutputMappings"/> instead.
        /// </summary>
        /// <param name="progressReporter">Progress reporter callback.</param>
        /// <returns>The built-in <see cref="AIOutputAdapterWorker"/>.</returns>
        protected sealed override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIOutputAdapterWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        /// <summary>
        /// Built-in worker that drives the AIOutputAdapterBase pipeline without requiring
        /// subclasses to implement their own worker. Reads the AIInputPayload tree and any
        /// additional inputs, then calls the AI for each branch and routes outputs via the
        /// output mappings.
        /// </summary>
        private sealed class AIOutputAdapterWorker : AsyncWorkerBase
        {
            private readonly AIOutputAdapterBase _parent;
            private readonly ProcessingOptions _processingOptions;
            private GH_Structure<GH_AIInputPayload> _payloadTree;
            private Dictionary<string, object> _additionalInputs;

            /// <inheritdoc/>
            public AIOutputAdapterWorker(
                AIOutputAdapterBase parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
                this._processingOptions = processingOptions;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this._payloadTree = new GH_Structure<GH_AIInputPayload>();
                DA.GetDataTree("Input >", out this._payloadTree);

                this._additionalInputs = new Dictionary<string, object>();
                this._parent.GatherAdditionalInputs(DA, this._additionalInputs);

                dataCount = 0;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[AIOutputAdapterWorker] DoWorkAsync start for {this._parent.GetType().Name}");

                    var mappings = this._parent.GetOutputMappings();
                    if (mappings == null || mappings.Count == 0)
                    {
                        Debug.WriteLine("[AIOutputAdapterWorker] No output mappings defined, aborting.");
                        return;
                    }

                    var primaryMapping = mappings[0];

                    // Run branch-by-branch processing directly on the typed payload tree.
                    // GH_AIInputPayload is IGH_Goo, so no conversion needed.
                    var processingResult = await this._parent.RunProcessingAsync(
                        new Dictionary<string, GH_Structure<GH_AIInputPayload>> { ["Input >"] = this._payloadTree },
                        async (branches) =>
                        {
                            return await this.ProcessBranchAsync(branches, token).ConfigureAwait(false);
                        },
                        this._processingOptions,
                        token).ConfigureAwait(false);

                    var resultTrees = processingResult;

                    // Extract primary output tree
                    GH_Structure<IGH_Goo> primaryTree;
                    if (!resultTrees.TryGetValue(primaryMapping.ParamName, out primaryTree))
                    {
                        primaryTree = new GH_Structure<IGH_Goo>();
                    }

                    // Build additional outputs list (all non-primary mappings)
                    var additionalOutputs = new List<(string name, object value)>();
                    foreach (var mapping in mappings.Skip(1))
                    {
                        if (resultTrees.TryGetValue(mapping.ParamName, out var extraTree))
                        {
                            additionalOutputs.Add((name: mapping.ParamName, value: (object)extraTree));
                        }
                    }

                    // Submit to batch or finalize synchronously
                    var batchSubmitted = await this._parent.TrySubmitBatchAsync(
                        primaryMapping.ParamName,
                        new Dictionary<string, GH_Structure<IGH_Goo>> { [primaryMapping.ParamName] = primaryTree },
                        token).ConfigureAwait(false);

                    if (!batchSubmitted)
                    {
                        this._parent.FinishResults(primaryMapping.ParamName, primaryTree, additionalOutputs.ToArray());
                    }

                    Debug.WriteLine($"[AIOutputAdapterWorker] DoWorkAsync complete for {this._parent.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIOutputAdapterWorker] Error: {ex.Message}");
                    this._parent.SetPersistentRuntimeMessage("adapter_worker_error", GH_RuntimeMessageLevel.Error, ex.Message, false);
                }
            }

            /// <summary>
            /// Processes a single branch: merges ALL payload items into one AIBody (one conversation)
            /// and makes a single API call. Called by DataTreeProcessor with BranchToBranch topology.
            /// </summary>
            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranchAsync(
                Dictionary<string, List<GH_AIInputPayload>> branches,
                CancellationToken token)
            {
                var mappings = this._parent.GetOutputMappings();
                var outputs = new Dictionary<string, List<IGH_Goo>>();
                foreach (var m in mappings) outputs[m.ParamName] = new List<IGH_Goo>();

                var payloadBranch = branches.TryGetValue("Input >", out var pb) ? pb : new List<GH_AIInputPayload>();
                if (payloadBranch.Count == 0) return outputs;

                // Merge the entire branch into one AIBody and build the system prompt.
                // Use a canonical path so PrepareInputs can look it up in MergePayloadsPerBranch.
                var canonicalPath = new GH_Path(0);
                var payloadTree = new GH_Structure<GH_AIInputPayload>();
                payloadTree.AppendRange(payloadBranch, canonicalPath);

                var inputs = new Dictionary<string, object> { ["Input >"] = payloadTree };

                // Inject additional gathered inputs into the per-branch inputs dict.
                // Additional inputs are scalar broadcasts (e.g. Voice, Speed, Schema) —
                // take the first available item from the first branch of the tree.
                foreach (var kvp in this._additionalInputs)
                {
                    if (kvp.Value is GH_Structure<IGH_Goo> tree && tree.DataCount > 0)
                    {
                        var firstBranch = tree.get_Branch(tree.Paths[0]);
                        var item = firstBranch?.Cast<IGH_Goo>().FirstOrDefault();
                        if (item != null) inputs[kvp.Key] = item;
                    }
                    else if (kvp.Value != null)
                    {
                        inputs[kvp.Key] = kvp.Value;
                    }
                }

                var context = new ProcessingUnitContext { Path = canonicalPath };
                this._parent.PrepareInputs(inputs, context);

                AIReturn aiResult = null;
                if (inputs.TryGetValue("_MergedBody", out var mergedBodyObj) && mergedBodyObj is AIBody mergedBody)
                {
                    aiResult = await this._parent.CallAIAsync(mergedBody, cancellationToken: token).ConfigureAwait(false);
                }

                var decodedMap = new Dictionary<string, IGH_Goo>();
                if (aiResult != null)
                {
                    foreach (var mapping in mappings)
                    {
                        decodedMap[mapping.ParamName] = mapping.Extractor(aiResult);
                    }
                }

                var transformed = this._parent.SentinelTransformOutputs(decodedMap, context);
                foreach (var mapping in mappings)
                {
                    transformed.TryGetValue(mapping.ParamName, out var val);
                    outputs[mapping.ParamName].Add(val);
                }

                return outputs;
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                // Outputs are persisted by FinishResults; RestorePersistentOutputs replays them.
                message = string.Empty;
            }
        }
    }
}
