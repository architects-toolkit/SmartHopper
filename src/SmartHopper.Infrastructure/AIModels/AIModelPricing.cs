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

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Pricing information for a model, in USD per unit as reported by the source
    /// of truth (OpenRouter). Token-based prices are per single token (e.g.
    /// <c>0.0000025m</c> means $2.50 per million tokens). All fields are optional;
    /// when <c>null</c> the concept is not priced or not published by the source.
    /// </summary>
    public class AIModelPricing
    {
        /// <summary>Price per prompt (input text) token.</summary>
        public decimal? Prompt { get; set; }

        /// <summary>Price per completion (output text) token.</summary>
        public decimal? Completion { get; set; }

        /// <summary>Flat per-request fee, if any.</summary>
        public decimal? Request { get; set; }

        /// <summary>Price per input image (legacy OpenRouter <c>image</c> field).</summary>
        public decimal? Image { get; set; }

        /// <summary>Price per generated (output) image.</summary>
        public decimal? ImageOutput { get; set; }

        /// <summary>Price per image token when images are charged per token.</summary>
        public decimal? ImageToken { get; set; }

        /// <summary>Price per input audio unit (legacy OpenRouter <c>audio</c> field).</summary>
        public decimal? Audio { get; set; }

        /// <summary>Price per output audio unit.</summary>
        public decimal? AudioOutput { get; set; }

        /// <summary>Price for cached input audio.</summary>
        public decimal? InputAudioCache { get; set; }

        /// <summary>Price per cached input token read.</summary>
        public decimal? InputCacheRead { get; set; }

        /// <summary>Price per cached input token write.</summary>
        public decimal? InputCacheWrite { get; set; }

        /// <summary>Price per internal reasoning token (hidden chain-of-thought).</summary>
        public decimal? InternalReasoning { get; set; }

        /// <summary>Price per web search invocation.</summary>
        public decimal? WebSearch { get; set; }

        /// <summary>Applied discount factor (0.0–1.0), if any.</summary>
        public decimal? Discount { get; set; }
    }
}
