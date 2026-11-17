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

namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Canonical vocabulary shared by <see cref="AIMetrics.Quantities"/> keys
    /// and <see cref="SmartHopper.ProviderSdk.AIModels.AIPriceEntry.Kind"/>:
    /// the catalogue wire, provider decoders, the cost calculator and the
    /// metrics serializers all speak these names.
    /// <para>
    /// Quantity keys are <c>"&lt;kind&gt;/&lt;measured-unit&gt;"</c> — the
    /// measured unit is what the provider reported (or SmartHopper measured),
    /// not necessarily the billed unit; the calculator converts at join time.
    /// </para>
    /// </summary>
    public static class AIQuantityKey
    {
        /// <summary>Prompt/input tokens billed as text.</summary>
        public const string TextInput = "text_input";

        /// <summary>Completion/output tokens billed as text.</summary>
        public const string TextOutput = "text_output";

        /// <summary>Prompt tokens served from the provider cache.</summary>
        public const string CacheRead = "cache_read";

        /// <summary>Tokens written to the provider prompt cache.</summary>
        public const string CacheWrite = "cache_write";

        /// <summary>Hidden reasoning tokens (chain-of-thought).</summary>
        public const string InternalReasoning = "internal_reasoning";

        /// <summary>Audio input (tokens or duration, depending on endpoint).</summary>
        public const string AudioInput = "audio_input";

        /// <summary>Audio output (tokens, characters or duration).</summary>
        public const string AudioOutput = "audio_output";

        /// <summary>Cached audio input.</summary>
        public const string AudioCacheRead = "audio_cache_read";

        /// <summary>Image input (tokens or items).</summary>
        public const string ImageInput = "image_input";

        /// <summary>Image output (tokens or items).</summary>
        public const string ImageOutput = "image_output";

        /// <summary>Video input (tokens or duration).</summary>
        public const string VideoInput = "video_input";

        /// <summary>Video output (tokens or duration).</summary>
        public const string VideoOutput = "video_output";

        /// <summary>Flat per-request fees.</summary>
        public const string Request = "request";

        /// <summary>Heuristic input-token estimate (provider silent on usage).</summary>
        public const string EstimatedTextInput = "estimated_text_input";

        /// <summary>Heuristic output-token estimate (provider silent on usage).</summary>
        public const string EstimatedTextOutput = "estimated_text_output";

        /// <summary>Kind namespace for billed tool invocations.</summary>
        public const string ToolPrefix = "tool:";

        /// <summary>Builds the open-ended <c>tool:&lt;name&gt;</c> kind.</summary>
        public static string Tool(string name) => ToolPrefix + name;

        /// <summary>Builds a quantity key: <c>"&lt;kind&gt;/&lt;unit&gt;"</c>.</summary>
        public static string Key(string kind, string unit) => kind + "/" + unit;

        /// <summary>Builds a quantity key from a price-unit wire spelling.</summary>
        public static string Key(string kind, SmartHopper.ProviderSdk.AIModels.AIPriceUnit unit)
            => kind + "/" + SmartHopper.ProviderSdk.AIModels.AIPriceEntry.ToWire(unit);
    }
}
