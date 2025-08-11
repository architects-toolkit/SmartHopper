/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents a fully-specified AI request that providers can execute, including
    /// provider information, model resolution, capability validation, and request body.
    /// </summary>
    public class AIRequestBase : IAIRequest
    {
        /// <summary>
        /// Store the desired model.
        /// </summary>
        private string? model;

        /// <inheritdoc/>
        public virtual string? Provider { get; set; }

        /// <inheritdoc/>
        public virtual IAIProvider ProviderInstance { get => ProviderManager.Instance.GetProvider(this.Provider); }

        /// <inheritdoc/>
        public virtual string Model { get => this.GetModelToUse(); set => this.model = value; }

        /// <inheritdoc/>
        public virtual AICapability Capability { get; set; } = AICapability.BasicChat;

        /// <inheritdoc/>
        public virtual (bool IsValid, List<string> Errors) IsValid()
        {
            var messages = new List<string>();
            bool hasErrors = false;

            if (string.IsNullOrEmpty(this.Provider))
            {
                messages.Add("Provider is required");
                hasErrors = true;
            }

            if (string.IsNullOrEmpty(this.model))
            {
                messages.Add($"(Info) Model is not specified - the default model '{this.GetModelToUse()}' will be used");
            }

            if (!this.Capability.HasInput() || !this.Capability.HasOutput())
            {
                messages.Add("Capability field is required with both input and output capabilities");
                hasErrors = true;
            }

            if (this.ProviderInstance == null)
            {
                messages.Add($"Unknown provider '{this.Provider}'");
                hasErrors = true;
            }

            if (!string.IsNullOrEmpty(this.model) && this.model != this.GetModelToUse())
            {
                messages.Add($"(Info) Model '{this.model}' is not capable for this request - the default model '{this.GetModelToUse()}' will be used");
            }

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public virtual Task<AIReturn> Exec()
        {
            throw new NotImplementedException("Exec() must be implemented in a derived class.");
        }

        /// <summary>
        /// Gets the model to use for the request.
        /// </summary>
        private string GetModelToUse()
        {
            if (string.IsNullOrEmpty(this.Provider))
            {
                return null;
            }

            var provider = this.ProviderInstance;
            if (provider == null)
            {
                return null;
            }

            var defaultModel = provider.GetDefaultModel(this.Capability);

            if (string.IsNullOrEmpty(this.model))
            {
                return defaultModel;
            }

            // Validate capabilities and return default if not capable
            if (!this.ValidModelCapabilities())
            {
                return defaultModel;
            }

            return this.model;
        }

        /// <summary>
        /// Validates the model capabilities and mentions the default model that will be used if the specified model is not capable to perform this request.
        /// </summary>
        private bool ValidModelCapabilities()
        {
            if (string.IsNullOrEmpty(this.Provider))
            {
                return false;
            }

            if (string.IsNullOrEmpty(this.model))
            {
                return false;
            }

            // Validate capabilities
            bool valid = ModelManager.Instance.ValidateCapabilities(this.Provider, this.model, this.Capability);
            return valid;
        }
    }
}
