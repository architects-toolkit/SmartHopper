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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmartHopper.ProviderSdk.AIModels
{
    /// <summary>
    /// Singleton registry of AI model capabilities keyed by "provider.model".
    /// Lives in the Provider SDK so providers can register/query capabilities without
    /// depending on the SmartHopper host. Thread-safe via ConcurrentDictionary and a
    /// coarse-grained lock around mutating operations that span multiple models
    /// (e.g. setting an exclusive default).
    /// </summary>
    public sealed class AIModelCapabilityRegistry
    {
        private static readonly Lazy<AIModelCapabilityRegistry> _instance =
            new Lazy<AIModelCapabilityRegistry>(() => new AIModelCapabilityRegistry());

        private readonly object _syncRoot = new object();

        /// <summary>
        /// Gets the process-wide singleton instance of the capability registry.
        /// </summary>
        public static AIModelCapabilityRegistry Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance. Prefer <see cref="Instance"/>; the public
        /// constructor exists primarily for unit-test scenarios.
        /// </summary>
        public AIModelCapabilityRegistry()
        {
        }

        /// <summary>
        /// Gets the dictionary of model capabilities keyed by "provider.model".
        /// Thread-safe: uses ConcurrentDictionary for reads/writes.
        /// </summary>
        public ConcurrentDictionary<string, AIModelCapabilities> Models { get; } = new ConcurrentDictionary<string, AIModelCapabilities>();

        #region Capability lookup

        /// <summary>
        /// Gets capabilities for a specific model.
        /// Enforces exact name or alias matching only. Wildcard patterns are not supported.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public AIModelCapabilities GetCapabilities(string provider, string model)
        {
            var key = $"{provider?.ToLowerInvariant()}.{model?.ToLowerInvariant()}";

            // Try exact match first (fastest path)
            if (this.Models.TryGetValue(key, out var capabilities))
            {
                return capabilities;
            }

            // Fallback: search by alias within the same provider
            var providerLower = provider?.ToLowerInvariant();
            var modelLower = model?.ToLowerInvariant();
            var byAlias = this.Models.Values
                .Where(m => m != null && m.Provider.Equals(providerLower, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(m => m.Aliases != null && m.Aliases.Any(a => string.Equals(a, modelLower, StringComparison.OrdinalIgnoreCase)));

            return byAlias;
        }

        /// <summary>
        /// Returns true when at least one model is registered for the specified provider.
        /// </summary>
        public bool HasProviderCapabilities(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return false;
            }

            return this.Models.Keys.Any(key => key.StartsWith($"{provider}.", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns whether the specified provider/model pair supports provider-side streaming.
        /// Unknown models are treated as not supporting streaming (safe fallback).
        /// </summary>
        public bool ModelSupportsStreaming(string provider, string model)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model))
            {
                return false;
            }

            var caps = this.GetCapabilities(provider, model);
            return caps != null && caps.SupportsStreaming;
        }

        #endregion

        #region Capability registration

        /// <summary>
        /// Adds or updates capabilities for a specific model.
        /// </summary>
        public void SetCapabilities(AIModelCapabilities capabilities)
        {
            if (capabilities == null) return;

            var key = capabilities.GetKey();
            lock (this._syncRoot)
            {
                this.Models[key] = capabilities;
            }
        }

        /// <summary>
        /// Convenience helper to register capabilities for a single (provider, model) pair.
        /// </summary>
        public void RegisterCapabilities(
            string provider,
            string modelName,
            AICapability capabilities,
            AICapability defaultFor = AICapability.None)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(modelName))
            {
                return;
            }

            this.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = modelName,
                Capabilities = capabilities,
                Default = defaultFor,
            });
        }

        /// <summary>
        /// Marks a model as the default for the given capabilities. When exclusive is true (default),
        /// clears those capabilities from Default on other models of the same provider so that there is
        /// at most one default per capability.
        /// </summary>
        public void SetDefault(string provider, string model, AICapability caps, bool exclusive = true)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model) || caps == AICapability.None)
            {
                return;
            }

            var normalizedProvider = provider.ToLowerInvariant();

            lock (this._syncRoot)
            {
                if (exclusive)
                {
                    foreach (var m in this.GetProviderModels(normalizedProvider))
                    {
                        if (!string.Equals(m.Model, model, StringComparison.Ordinal))
                        {
                            var before = m.Default;
                            m.Default &= ~caps;
                            if (m.Default != before)
                            {
                                this.Models[m.GetKey()] = m;
                            }
                        }
                    }
                }

                var target = this.GetCapabilities(normalizedProvider, model) ?? new AIModelCapabilities
                {
                    Provider = normalizedProvider,
                    Model = model,
                    Capabilities = AICapability.None,
                    Default = AICapability.None,
                };

                target.Default |= caps;
                this.Models[target.GetKey()] = target;
            }
        }

        #endregion

        #region Model listing and selection

        /// <summary>
        /// Returns all registered models for a provider.
        /// </summary>
        public List<AIModelCapabilities> GetProviderModels(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return new List<AIModelCapabilities>();
            }

            return this.Models.Values
                .Where(m => m != null && string.Equals(m.Provider, provider, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Gets all models that support the specified capabilities.
        /// </summary>
        public List<AIModelCapabilities> FindModelsWithCapabilities(AICapability requiredCapabilities)
        {
            if (requiredCapabilities == AICapability.None)
            {
                return this.Models.Values.ToList();
            }

            return this.Models.Values
                .Where(model => model.HasCapability(requiredCapabilities))
                .ToList();
        }

        /// <summary>
        /// Gets the default model for a provider and specific capability.
        /// </summary>
        public string GetDefaultModel(string provider, AICapability requiredCapability = AICapability.Text2Text)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return string.Empty;
            }

            var providerModels = this.GetProviderModels(provider);
            if (providerModels.Count == 0)
            {
                return string.Empty;
            }

            var exact = providerModels
                .Where(m => (m.Default & requiredCapability) == requiredCapability)
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (exact != null)
            {
                return exact.Model;
            }

            var compatible = providerModels
                .Where(m => m.Default != AICapability.None && m.HasCapability(requiredCapability))
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return compatible?.Model ?? string.Empty;
        }

        /// <summary>
        /// DO NOT CALL THIS DIRECTLY. USE AIProvider.SelectModel() INSTEAD.
        /// Centralized model selection and fallback logic.
        /// </summary>
        public string SelectBestModel(string provider, string userModel, AICapability requiredCapability, string preferredDefault = null)
        {
            if (string.IsNullOrWhiteSpace(provider) || requiredCapability == AICapability.None)
            {
                Debug.WriteLine($"[AIModelCapabilityRegistry.SelectBestModel] Invalid args -> provider='{provider}', required='{requiredCapability.ToDetailedString()}'");
                return string.Empty;
            }

            // If user specified a model, validate capability if known; allow unknown to pass
            if (!string.IsNullOrWhiteSpace(userModel))
            {
                var known = this.GetCapabilities(provider, userModel);
                if (known == null)
                {
                    return userModel;
                }

                if (known.HasCapability(requiredCapability))
                {
                    return userModel;
                }
            }

            var candidates = this.GetProviderModels(provider)
                .Where(m => m.HasCapability(requiredCapability))
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredDefault))
            {
                var preferredCaps = this.GetCapabilities(provider, preferredDefault);
                if (preferredCaps != null && preferredCaps.HasCapability(requiredCapability))
                {
                    return preferredDefault;
                }
            }

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            // 1) Defaults with exact capability
            var defaultExact = candidates
                .Where(m => (m.Default & requiredCapability) == requiredCapability)
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (defaultExact != null)
            {
                return defaultExact.Model;
            }

            // 2) Other defaults that are compatible
            var defaultCompatible = candidates
                .Where(m => m.Default != AICapability.None)
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (defaultCompatible != null)
            {
                return defaultCompatible.Model;
            }

            // 3) Any other registered model, ordered by quality
            var best = candidates
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return best?.Model ?? string.Empty;
        }

        #endregion

        #region Capability validation

        /// <summary>
        /// Validates whether a model registered for the given provider has the required capabilities.
        /// Unknown (unregistered) models bypass validation and are treated as compatible — the request
        /// layer is responsible for additional checks.
        /// </summary>
        public bool ValidateCapabilities(string provider, string model, AICapability requiredCapability)
        {
            var capabilities = this.GetCapabilities(provider, model);
            if (capabilities == null)
            {
                return true;
            }

            return capabilities.HasCapability(requiredCapability);
        }

        #endregion
    }
}
