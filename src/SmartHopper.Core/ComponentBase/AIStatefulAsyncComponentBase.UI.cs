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
using System.Linq;
using SmartHopper.Core.ComponentBase.Attributes;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.State;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// UI and badge rendering logic for AIStatefulAsyncComponentBase.
    /// Handles component attributes, badge caching, and state messages.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
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

                var req = new SmartHopper.ProviderSdk.AICall.Core.Requests.AIRequestCall();
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
                    m.Severity == SHRuntimeMessageSeverity.Error &&
                    (m.Code == SHMessageCode.ProviderMissing)) == true;

                bool hasUnknownProvider = validationMessages?.Any(m =>
                    m.Severity == SHRuntimeMessageSeverity.Error &&
                    (m.Code == SHMessageCode.UnknownProvider)) == true;

                bool hasNoCapableModel = validationMessages?.Any(m =>
                    m.Severity == SHRuntimeMessageSeverity.Error &&
                    (m.Code == SHMessageCode.NoCapableModel)) == true;

                bool hasUnknownModel = validationMessages?.Any(m =>
                    (m.Code == SHMessageCode.UnknownModel)) == true;

                bool hasCapabilityMismatch = validationMessages?.Any(m =>
                    (m.Code == SHMessageCode.CapabilityMismatch)) == true;

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
                var resolvedCaps = string.IsNullOrWhiteSpace(resolvedModel) ? null : AIModelCapabilityRegistry.Instance.GetCapabilities(providerName, resolvedModel);
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
            if (this._batchState.Submission != null)
            {
                var total = this._batchState.Submission.CustomIds?.Count ?? 0;
                return $"Processing batch ({this._batchState.ProgressCompleted}/{total})...";
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
    }
}
