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
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Static registry and resolver for modality fallbacks.
    /// </summary>
    public sealed class ModalityFallbackResolver
    {
        private static readonly List<IModalityFallback> RegisteredFallbacks = new();
        private static bool _initialized;

        /// <summary>
        /// Ensures built-in fallbacks are registered. Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            Register(new ImageToTextFallback());
            Register(new AudioToTextFallback());
        }

        /// <summary>
        /// Registers a fallback handler. Called at Infrastructure load time.
        /// </summary>
        public static void Register(IModalityFallback fallback)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
            RegisteredFallbacks.Add(fallback);
            Logger.For<ModalityFallbackResolver>().Trace($"[FallbackResolver] Registered: {fallback.Name} (handles {fallback.Handles})");
        }

        /// <summary>
        /// Gets all registered fallbacks. Used by Settings UI for provider pin rows.
        /// </summary>
        public static IReadOnlyList<IModalityFallback> GetRegisteredFallbacks() => RegisteredFallbacks;

        /// <summary>
        /// Resolves a single conversion step — provider and model — for a fallback,
        /// honoring provider pins, the fallback mode, and provider availability.
        /// Unlike <see cref="Resolve"/>, this works for unregistered fallbacks (e.g. output
        /// passthrough conversions such as TTS) and prefers the component's own model when
        /// it already satisfies the conversion's capability.
        /// </summary>
        /// <param name="fallback">The conversion to run.</param>
        /// <param name="componentProvider">The component's configured provider.</param>
        /// <param name="componentModel">The component's configured model (preferred when capable).</param>
        /// <param name="mode">The effective fallback mode.</param>
        /// <returns>A resolved <see cref="FallbackStep"/>, or null when nothing can run it.</returns>
        public FallbackStep ResolveStep(
            IModalityFallback fallback,
            string componentProvider,
            string componentModel,
            ModalityFallbackMode mode)
        {
            EnsureInitialized();
            var settings = SmartHopperSettings.Load();
            var pins = settings.FallbackProviderPins ?? new Dictionary<string, FallbackProviderPin>();

            return TryResolveConversionTargets(
                fallback, componentProvider, componentModel, mode, pins,
                out var candidates, out _)
                ? new FallbackStep(fallback, candidates)
                : null;
        }

        /// <summary>
        /// Finds a chain that converts unsupported capabilities into supported ones.
        /// Returns null if no chain can cover all missing capabilities.
        /// </summary>
        /// <param name="providerName">The component's configured provider.</param>
        /// <param name="modelName">The component's configured model.</param>
        /// <param name="required">The required capability flags.</param>
        /// <param name="mode">The active fallback mode.</param>
        /// <returns>A resolved <see cref="FallbackChain"/> or null.</returns>
        public FallbackChain Resolve(
            string providerName,
            string modelName,
            AICapability required,
            ModalityFallbackMode mode)
        {
            EnsureInitialized();
            if (mode == ModalityFallbackMode.Disabled) return null;

            var modelCaps = AIModelCapabilityRegistry.Instance.GetCapabilities(providerName, modelName);
            if (modelCaps != null && modelCaps.HasCapability(required))
            {
                return null; // model already supports everything
            }

            // Compute missing flags
            var supported = modelCaps?.Capabilities ?? AICapability.None;
            var missing = required & ~supported;
            if (missing == AICapability.None) return null;

            var settings = SmartHopperSettings.Load();
            return this.Resolve(providerName, modelName, required, mode, settings);
        }

        /// <summary>
        /// Internal seam: resolve against an explicit settings snapshot so deterministic
        /// tests never touch persisted user settings.
        /// </summary>
        internal FallbackChain Resolve(string providerName, string modelName, AICapability required, ModalityFallbackMode mode, SmartHopperSettings settings)
        {
            var modelCaps = AIModelCapabilityRegistry.Instance.GetCapabilities(providerName, modelName);
            if (modelCaps != null && modelCaps.HasCapability(required))
            {
                return null; // model already supports everything
            }

            // Compute missing flags
            var supported = modelCaps?.Capabilities ?? AICapability.None;
            var missing = required & ~supported;
            if (missing == AICapability.None) return null;

            var pins = settings?.FallbackProviderPins ?? new Dictionary<string, FallbackProviderPin>();
            var steps = new List<FallbackStep>();
            bool usesAlt = false;

            // For each missing capability flag, find a fallback. Each step keeps its own
            // resolved provider/model so a multi-modality chain may span providers.
            foreach (AICapability flag in Enum.GetValues(typeof(AICapability)))
            {
                if (flag == AICapability.None) continue;
                if ((missing & flag) != flag) continue;
                // Skip composite flags — only process leaf flags
                if (!IsSingleBitFlag(flag)) continue;

                var fallback = FindFallbackForFlag(flag, providerName, mode, pins, out var candidates, out var isPinned);
                if (fallback == null) return null; // cannot cover this missing flag
                if (candidates.Count == 0) return null; // no model can perform the conversion

                steps.Add(new FallbackStep(fallback, candidates));
                if (candidates.Any(c => !string.Equals(c.Provider, providerName, StringComparison.OrdinalIgnoreCase)))
                {
                    usesAlt = true;
                }
            }

            if (steps.Count == 0) return null;

            var description = string.Join("; ", steps.Select(s => s.Fallback.Description));
            var effectiveCap = required;
            foreach (var s in steps)
            {
                effectiveCap = (effectiveCap & ~s.Fallback.Handles) | s.Fallback.ResultsIn;
            }

            return new FallbackChain(steps, description, effectiveCap)
            {
                UsesAltProvider = usesAlt,
            };
        }

        /// <summary>
        /// Resolves ONE conversion step for an explicit enrichment call. Unlike
        /// <see cref="Resolve(string, string, AICapability, ModalityFallbackMode)"/>, this
        /// is for caller-requested paid conversion: when the generic fallback mode is
        /// Disabled, resolution still proceeds as ConfiguredProvider because the user
        /// explicitly asked for AI conversion. A preferred provider is tried first; a
        /// preferred model is used directly when the registry confirms the conversion
        /// capability. Configured pins still apply.
        /// </summary>
        /// <param name="conversionCapability">The capability to convert FROM
        /// (e.g. <see cref="AICapability.ImageInput"/>).</param>
        /// <param name="preferredProvider">Preferred helper provider (tried first).</param>
        /// <param name="preferredModel">Preferred helper model, used directly when the
        /// registry confirms it can perform the conversion.</param>
        /// <param name="mode">Active fallback mode; Disabled upgrades to ConfiguredProvider
        /// for this explicit-requested conversion only.</param>
        /// <returns>A single-step <see cref="FallbackChain"/>, or null when no permitted
        /// helper can perform the conversion.</returns>
        public FallbackChain ResolveConversion(
            AICapability conversionCapability,
            string preferredProvider,
            string preferredModel,
            ModalityFallbackMode mode)
        {
            return ResolveConversion(
                conversionCapability, preferredProvider, preferredModel, mode,
                SmartHopperSettings.Load());
        }

        /// <summary>
        /// Internal test seam: resolve against a caller-supplied settings snapshot
        /// instead of loading persisted user settings.
        /// </summary>
        internal FallbackChain ResolveConversion(
            AICapability conversionCapability,
            string preferredProvider,
            string preferredModel,
            ModalityFallbackMode mode,
            SmartHopperSettings settings)
        {
            EnsureInitialized();

            var effectiveMode = mode == ModalityFallbackMode.Disabled
                ? ModalityFallbackMode.ConfiguredProvider
                : mode;

            var pins = settings?.FallbackProviderPins ?? new Dictionary<string, FallbackProviderPin>();

            var fallback = FindFallbackForFlag(
                conversionCapability, preferredProvider, effectiveMode, pins,
                out var candidates, out var isPinned);
            if (fallback == null)
            {
                return null;
            }

            // The preferred model wins only when no explicit pin resolved — pins are
            // authoritative and must not be overridden by a caller preference.
            if (!isPinned && !string.IsNullOrWhiteSpace(preferredModel)
                && !string.IsNullOrWhiteSpace(preferredProvider)
                && AIModelCapabilityRegistry.Instance.ValidateCapabilities(
                    preferredProvider, preferredModel, fallback.RequiresCapability))
            {
                candidates.RemoveAll(c => string.Equals(c.Provider, preferredProvider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(c.Model, preferredModel, StringComparison.OrdinalIgnoreCase));
                candidates.Insert(0, (preferredProvider, preferredModel));
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var step = new FallbackStep(fallback, candidates);
            var effectiveCap = (conversionCapability & ~fallback.Handles) | fallback.ResultsIn;
            return new FallbackChain(new List<FallbackStep> { step }, fallback.Description, effectiveCap)
            {
                UsesAltProvider = !string.Equals(
                    step.Provider, preferredProvider, StringComparison.OrdinalIgnoreCase),
            };
        }

        private static IModalityFallback FindFallbackForFlag(
            AICapability flag,
            string componentProvider,
            ModalityFallbackMode mode,
            Dictionary<string, FallbackProviderPin> pins,
            out List<(string Provider, string Model)> candidates,
            out bool isPinned)
        {
            candidates = new List<(string Provider, string Model)>();
            isPinned = false;

            foreach (var fb in RegisteredFallbacks)
            {
                if (fb.Handles != flag) continue;

                if (pins.TryGetValue(fb.Name, out var pin) && pin != null && !string.IsNullOrWhiteSpace(pin.Provider))
                {
                    isPinned = true;
                }

                if (TryResolveConversionTargets(
                        fb, componentProvider, null, mode, pins,
                        out candidates, out var pinFailed))
                {
                    return fb;
                }

                // A pin that targets an unavailable provider fails the flag outright —
                // automatic selection must not silently override the user's pin.
                if (pinFailed) return null;
            }

            return null;
        }

        /// <summary>
        /// Resolves the provider/model that should execute one conversion, honoring
        /// pin overrides, the fallback mode, and provider availability. When the
        /// component's provider is usable, <paramref name="componentModel"/> is preferred
        /// over <see cref="AIModelCapabilityRegistry.SelectBestModel"/> when it already
        /// satisfies the conversion's capability.
        /// </summary>
        /// <param name="fb">The conversion to run.</param>
        /// <param name="componentProvider">The component's configured provider.</param>
        /// <param name="componentModel">Preferred model, or null for automatic selection.</param>
        /// <param name="mode">The effective fallback mode.</param>
        /// <param name="pins">User-configured provider pins keyed by fallback name.</param>
        /// <param name="candidates">Ordered (provider, model) candidates, most-preferred
        /// first. May hold alternates for execution failover; explicit pins and
        /// <see cref="ModalityFallbackMode.Disabled"/> yield at most one.</param>
        /// <param name="pinFailed">True when a configured pin cannot run the conversion.</param>
        private static bool TryResolveConversionTargets(
            IModalityFallback fb,
            string componentProvider,
            string componentModel,
            ModalityFallbackMode mode,
            Dictionary<string, FallbackProviderPin> pins,
            out List<(string Provider, string Model)> candidates,
            out bool pinFailed)
        {
            var list = new List<(string Provider, string Model)>();
            candidates = list;
            pinFailed = false;

            void AddCandidate(string provider, string model)
            {
                if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model))
                {
                    return;
                }

                if (list.Any(c => string.Equals(c.Provider, provider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(c.Model, model, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                list.Add((provider, model));
            }

            // Pin override — single authoritative candidate; a failed pin fails the
            // conversion rather than falling back silently.
            if (pins != null
                && pins.TryGetValue(fb.Name, out var pin)
                && pin != null
                && !string.IsNullOrWhiteSpace(pin.Provider))
            {
                if (!fb.IsAvailable(pin.Provider))
                {
                    Logger.For<ModalityFallbackResolver>().Warning($"[FallbackResolver] Pinned provider '{pin.Provider}' cannot perform {fb.Name}");
                    pinFailed = true;
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(pin.Model))
                {
                    // An explicit pin must itself confirm the conversion capability —
                    // request model selection must never silently substitute another model.
                    var pinCapabilities = AIModelCapabilityRegistry.Instance.GetCapabilities(pin.Provider, pin.Model);
                    if (pinCapabilities == null || !pinCapabilities.HasCapability(fb.RequiresCapability))
                    {
                        Logger.For<ModalityFallbackResolver>().Warning($"[FallbackResolver] Pinned model '{pin.Provider}' / '{pin.Model}' cannot perform {fb.Name} (requires {fb.RequiresCapability.ToDetailedString()})");
                        pinFailed = true;
                        return false;
                    }

                    AddCandidate(pin.Provider, pin.Model);
                }
                else
                {
                    AddCandidate(pin.Provider, AIModelCapabilityRegistry.Instance.SelectBestModel(pin.Provider, null, fb.RequiresCapability));
                }

                return candidates.Count > 0;
            }

            if (mode == ModalityFallbackMode.Disabled)
            {
                // Passthrough conversions are the component's own function: "disabled" forbids
                // substituting another provider or model, but the configured model may still run.
                if (!string.IsNullOrWhiteSpace(componentProvider)
                    && !string.IsNullOrWhiteSpace(componentModel)
                    && ProviderSdkHost.ProviderRegistry.GetProvider(componentProvider)?.IsConfigured == true
                    && AIModelCapabilityRegistry.Instance.ValidateCapabilities(componentProvider, componentModel, fb.RequiresCapability))
                {
                    AddCandidate(componentProvider, componentModel);
                }

                return candidates.Count > 0;
            }

            // Component's provider first — the preferred (component) model when it satisfies
            // the capability, plus the best catalogued model as an execution alternate.
            if (fb.IsAvailable(componentProvider))
            {
                if (!string.IsNullOrWhiteSpace(componentModel)
                    && AIModelCapabilityRegistry.Instance.ValidateCapabilities(componentProvider, componentModel, fb.RequiresCapability))
                {
                    AddCandidate(componentProvider, componentModel);
                }

                AddCandidate(componentProvider, AIModelCapabilityRegistry.Instance.SelectBestModel(componentProvider, null, fb.RequiresCapability));
            }

            if (mode == ModalityFallbackMode.AnyProvider)
            {
                // Then one best candidate per other configured provider, in registry order.
                // How many alternates actually run is bounded by the chain's attempt policy.
                foreach (var provider in ProviderSdkHost.ProviderRegistry.GetProviders())
                {
                    if (string.Equals(provider.Name, componentProvider, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!fb.IsAvailable(provider.Name)) continue;

                    AddCandidate(provider.Name, AIModelCapabilityRegistry.Instance.SelectBestModel(provider.Name, null, fb.RequiresCapability));
                }
            }

            return candidates.Count > 0;
        }

        private static bool IsSingleBitFlag(AICapability flag)
        {
            var v = (int)flag;
            return v != 0 && (v & (v - 1)) == 0;
        }
    }
}
