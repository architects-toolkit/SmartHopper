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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Validates that a component's required capability is supported by the configured provider and model.
    /// Provides both synchronous and asynchronous validation with fallback chain detection.
    /// </summary>
    public sealed class ComponentCapabilityValidator : IValidator<AICapability>
    {
        private readonly string _providerName;
        private readonly string _modelName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentCapabilityValidator"/> class.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to validate.</param>
        /// <param name="modelName">The name of the model to validate (can be null or empty for default).</param>
        public ComponentCapabilityValidator(string providerName, string modelName = null)
        {
            this._providerName = providerName;
            this._modelName = modelName;
        }

        /// <summary>
        /// Gets the severity threshold at or above which validation fails.
        /// </summary>
        public SHRuntimeMessageSeverity FailOn { get; } = SHRuntimeMessageSeverity.Error;

        /// <summary>
        /// Validates the capability synchronously (no async I/O).
        /// This is the preferred method for pre-validation in SolveInstance.
        /// </summary>
        /// <param name="capability">The capability to validate.</param>
        /// <returns>Validation result with IsValid and optional fallback description.</returns>
        public ValidationResult ValidateSync(AICapability capability)
        {
            var messages = new List<SHRuntimeMessage>();

            // Step 1: Check provider is registered and enabled
            var provider = ProviderManager.Instance.GetProvider(this._providerName);
            if (provider == null)
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.ProviderMissing,
                    $"Provider '{this._providerName}' is not registered or enabled."));

                return new ValidationResult
                {
                    IsValid = false,
                    Messages = messages,
                };
            }

            // Step 2: Resolve effective model
            var effectiveModel = string.IsNullOrWhiteSpace(this._modelName)
                ? provider.GetDefaultModel()
                : this._modelName;

            if (string.IsNullOrWhiteSpace(effectiveModel))
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.UnknownModel,
                    $"No model could be resolved for provider '{this._providerName}'."));

                return new ValidationResult
                {
                    IsValid = false,
                    Messages = messages,
                };
            }

            // Step 3: Check capability support
            var modelManager = AIModelCapabilityRegistry.Instance;
            var supportsCapability = modelManager.ValidateCapabilities(this._providerName, effectiveModel, capability);

            if (supportsCapability)
            {
                // Capability is fully supported
                return new ValidationResult
                {
                    IsValid = true,
                    Messages = messages,
                };
            }

            // Capability not supported - return error
            messages.Add(new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Validation,
                SHMessageCode.CapabilityMismatch,
                $"Provider '{this._providerName}' / model '{effectiveModel}' does not support {capability}."));

            return new ValidationResult
            {
                IsValid = false,
                Messages = messages,
            };
        }

        /// <summary>
        /// Validates the capability asynchronously.
        /// Delegates to ValidateSync for now (no async I/O required).
        /// </summary>
        /// <param name="instance">The capability to validate.</param>
        /// <param name="context">Validation context (unused).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        public Task<ValidationResult> ValidateAsync(AICapability instance, ValidationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = this.ValidateSync(instance);
            return Task.FromResult(result);
        }
    }
}
