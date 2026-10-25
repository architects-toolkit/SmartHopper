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
using SmartHopper.ProviderSdk.AIModels;
namespace SmartHopper.ProviderSdk.Catalogue
{
    /// <summary>
    /// Trust anchors and constant mappings for the public model catalogue
    /// (<c>models.smarthopper.xyz</c>, protocol <c>smarthopper-models/1</c>).
    /// </summary>
    public static class ModelCatalogueTrust
    {
        /// <summary>Public API base URL for the model catalogue service.</summary>
        public const string DefaultBaseUrl = "https://models.smarthopper.xyz";

        /// <summary>
        /// Catalogue public keys (SPKI DER, base64) embedded at build time, keyed
        /// by <c>key_id</c>. Until the first production key is generated this set
        /// is empty — verification fails closed, so the plugin keeps working on
        /// the embedded seed and any already-trusted local cache. Local
        /// development can inject keys via the <c>SMARTHOPPER_CATALOGUE_KEYS</c>
        /// environment variable (<c>keyid=base64spki;keyid2=base64spki2</c>).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> EmbeddedKeys = new Dictionary<string, string>
        {
            { "0570629f4d96bba5", "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdQNKafQa+ww7bWtUbT7MX6cTN0Jc0cocVoTnwshMlxhmIec89x7rl7+TODNkKe0bb1YBDELNoko99m1hR6pkEw==" },
        };

        /// <summary>
        /// Wire provider id → plugin provider name (lowercased, as registered in
        /// <see cref="AIModelCapabilityRegistry"/>). Unknown ids map to null and
        /// their sections are ignored (forward compatibility). Provider names are
        /// normalized exactly like the registry does: <c>Name.ToLowerInvariant()</c>.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> PluginProviderNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openai"] = "openai",
                ["anthropic"] = "anthropic",
                ["mistralai"] = "mistralai",
                ["deepseek"] = "deepseek",
                ["gemini"] = "gemini",
                ["openrouter"] = "openrouter",
            };

        /// <summary>
        /// Maps a wire provider id to the plugin provider name, or null when the
        /// catalogue section belongs to a provider this plugin does not ship.
        /// </summary>
        /// <param name="wireProviderId">Wire provider id (e.g. <c>openai</c>).</param>
        public static string ToPluginProviderName(string wireProviderId)
        {
            if (wireProviderId == null)
            {
                return null;
            }

            return PluginProviderNames.TryGetValue(wireProviderId, out var name) ? name : null;
        }

        /// <summary>
        /// Short capability tokens accepted on the wire. Canonical
        /// <see cref="AICapability"/> names are also accepted by the parser;
        /// unknown tokens are ignored.
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, AICapability> ShortTokenCapabilities =
            new Dictionary<string, AICapability>(StringComparer.Ordinal)
            {
                ["text"] = AICapability.TextInput | AICapability.TextOutput,
                ["json"] = AICapability.JsonOutput,
                ["tools"] = AICapability.FunctionCalling,
                ["image"] = AICapability.ImageInput,
                ["image_out"] = AICapability.ImageOutput,
                ["reasoning"] = AICapability.Reasoning,
                ["audio_in"] = AICapability.AudioInput,
                ["audio_out"] = AICapability.AudioOutput,
                ["speech_in"] = AICapability.SpeechInput,
                ["speech_out"] = AICapability.SpeechOutput,
                ["video_in"] = AICapability.VideoInput,
                ["video_out"] = AICapability.VideoOutput,
                ["embed"] = AICapability.EmbedOutput,
            };
    }
}
