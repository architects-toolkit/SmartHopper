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
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;

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
            Debug.WriteLine($"[FallbackResolver] Registered: {fallback.Name} (handles {fallback.Handles})");
        }

        /// <summary>
        /// Gets all registered fallbacks. Used by Settings UI for provider pin rows.
        /// </summary>
        public static IReadOnlyList<IModalityFallback> GetRegisteredFallbacks() => RegisteredFallbacks;

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

            var modelCaps = ModelManager.Instance.GetCapabilities(providerName, modelName);
            if (modelCaps != null && modelCaps.HasCapability(required))
            {
                return null; // model already supports everything
            }

            // Compute missing flags
            var supported = modelCaps?.Capabilities ?? AICapability.None;
            var missing = required & ~supported;
            if (missing == AICapability.None) return null;

            var settings = SmartHopperSettings.Load();
            var pins = settings.FallbackProviderPins ?? new Dictionary<string, FallbackProviderPin>();
            var steps = new List<IModalityFallback>();
            string chainProvider = null;
            string chainModel = null;
            bool usesAlt = false;

            // For each missing capability flag, find a fallback
            foreach (AICapability flag in Enum.GetValues(typeof(AICapability)))
            {
                if (flag == AICapability.None) continue;
                if ((missing & flag) != flag) continue;
                // Skip composite flags — only process leaf flags
                if (!IsSingleBitFlag(flag)) continue;

                var fallback = FindFallbackForFlag(flag, providerName, mode, pins, out var resolvedProvider, out var resolvedModel, out var isPinned);
                if (fallback == null) return null; // cannot cover this missing flag

                steps.Add(fallback);
                chainProvider = resolvedProvider;
                chainModel = resolvedModel;
                if (!string.Equals(resolvedProvider, providerName, StringComparison.OrdinalIgnoreCase))
                {
                    usesAlt = true;
                }
            }

            if (steps.Count == 0) return null;

            var description = string.Join("; ", steps.Select(s => s.Description));
            var effectiveCap = required;
            foreach (var s in steps)
            {
                effectiveCap = (effectiveCap & ~s.Handles) | s.ResultsIn;
            }

            return new FallbackChain(steps, description, chainProvider, chainModel, effectiveCap)
            {
                UsesAltProvider = usesAlt,
            };
        }

        private static IModalityFallback FindFallbackForFlag(
            AICapability flag,
            string componentProvider,
            ModalityFallbackMode mode,
            Dictionary<string, FallbackProviderPin> pins,
            out string resolvedProvider,
            out string resolvedModel,
            out bool isPinned)
        {
            resolvedProvider = null;
            resolvedModel = null;
            isPinned = false;

            foreach (var fb in RegisteredFallbacks)
            {
                if (fb.Handles != flag) continue;

                // Check for a pin override
                if (pins.TryGetValue(fb.Name, out var pin) && pin != null && !string.IsNullOrWhiteSpace(pin.Provider))
                {
                    isPinned = true;
                    if (!fb.IsAvailable(pin.Provider))
                    {
                        Debug.WriteLine($"[FallbackResolver] Pinned provider '{pin.Provider}' cannot perform {fb.Name}");
                        return null;
                    }

                    resolvedProvider = pin.Provider;
                    resolvedModel = pin.Model
                        ?? ModelManager.Instance.SelectBestModel(pin.Provider, null, fb.RequiresCapability);
                    return fb;
                }

                // No pin — use automatic selection
                if (mode == ModalityFallbackMode.ConfiguredProvider)
                {
                    if (fb.IsAvailable(componentProvider))
                    {
                        resolvedProvider = componentProvider;
                        resolvedModel = ModelManager.Instance.SelectBestModel(componentProvider, null, fb.RequiresCapability);
                        return fb;
                    }
                }
                else if (mode == ModalityFallbackMode.AnyProvider)
                {
                    // Try the component's provider first
                    if (fb.IsAvailable(componentProvider))
                    {
                        resolvedProvider = componentProvider;
                        resolvedModel = ModelManager.Instance.SelectBestModel(componentProvider, null, fb.RequiresCapability);
                        return fb;
                    }

                    // Then try all configured providers
                    foreach (var provider in ProviderManager.Instance.GetProviders())
                    {
                        if (string.Equals(provider.Name, componentProvider, StringComparison.OrdinalIgnoreCase)) continue;
                        if (fb.IsAvailable(provider.Name))
                        {
                            resolvedProvider = provider.Name;
                            resolvedModel = ModelManager.Instance.SelectBestModel(provider.Name, null, fb.RequiresCapability);
                            return fb;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsSingleBitFlag(AICapability flag)
        {
            var v = (int)flag;
            return v != 0 && (v & (v - 1)) == 0;
        }
    }
}
