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

namespace SmartHopper.ProviderSdk.AIModels
{
    /// <summary>
    /// Pricing information for a model, in its declared currency per unit.
    /// USD remains the default for OpenRouter. Token prices are per single token (e.g.
    /// <c>0.0000025m</c> means $2.50 per million tokens). Names follow the
    /// <c>&lt;modality&gt;_&lt;direction&gt;</c> convention shared with the catalogue wire
    /// schema (<c>text_input</c>, <c>image_output</c>, <c>cache_read</c>, …): input-side
    /// fields carry the <c>Input</c> suffix direction, output-side fields carry
    /// <c>Output</c>, and prompt-cache fields are <c>CacheRead</c>/<c>CacheWrite</c>.
    /// All fields are optional; when <c>null</c> the concept is not priced or not
    /// published by the source.
    /// </summary>
    public class AIModelPricing
    {
        /// <summary>Gets or sets the currency of all prices; defaults to USD for existing providers.</summary>
        public string Currency { get; set; } = "USD";

        /// <summary>Price per text input token (prompt).</summary>
        public decimal? TextInput { get; set; }

        /// <summary>Price per text output token (completion).</summary>
        public decimal? TextOutput { get; set; }

        /// <summary>Flat per-request fee, if any.</summary>
        public decimal? Request { get; set; }

        /// <summary>Price per input image (legacy OpenRouter <c>image</c> field).</summary>
        public decimal? ImageInput { get; set; }

        /// <summary>Price per generated (output) image.</summary>
        public decimal? ImageOutput { get; set; }

        /// <summary>Price per image token when images are charged per token.</summary>
        public decimal? ImageToken { get; set; }

        /// <summary>Price per input audio unit (legacy OpenRouter <c>audio</c> field).</summary>
        public decimal? AudioInput { get; set; }

        /// <summary>Price per output audio unit.</summary>
        public decimal? AudioOutput { get; set; }

        /// <summary>Price per generated (output) video unit, as declared by the feed.</summary>
        public decimal? VideoOutput { get; set; }

        /// <summary>Price for cached input audio.</summary>
        public decimal? AudioCacheRead { get; set; }

        /// <summary>Price per cached prompt-cache read token.</summary>
        public decimal? CacheRead { get; set; }

        /// <summary>Price per token written to the provider's prompt cache (e.g. Anthropic cache creation).</summary>
        public decimal? CacheWrite { get; set; }

        /// <summary>Price per internal reasoning token (hidden chain-of-thought).</summary>
        public decimal? InternalReasoning { get; set; }

        /// <summary>Price per web search invocation.</summary>
        public decimal? WebSearch { get; set; }

        /// <summary>Applied discount factor (0.0–1.0), if any.</summary>
        public decimal? Discount { get; set; }
    }
}
