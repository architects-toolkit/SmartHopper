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

using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.ProviderSdk.AIModels
{
    /// <summary>
    /// Pricing information for a model: a currency plus an open-ended list of
    /// <see cref="AIPriceEntry"/> records. Entries-first since
    /// <c>smarthopper-models/2</c> — every entry declares its
    /// <see cref="AIPriceEntry.Kind"/> (canonical kinds live in
    /// <c>AIQuantityKey</c>, <c>tool:&lt;name&gt;</c> covers tool fees) and
    /// its <see cref="AIPriceEntry.Unit"/> explicitly, so per-token,
    /// per-minute, per-item and per-invocation billing share one shape.
    /// Amounts are per single unit in <see cref="Currency"/>.
    /// </summary>
    public class AIModelPricing
    {
        /// <summary>Gets or sets the ISO-4217 currency of all entry amounts (e.g. <c>USD</c>, <c>EUR</c>).</summary>
        public string Currency { get; set; } = "USD";

        /// <summary>Gets or sets the applied discount factor (0.0–1.0), if any.</summary>
        public decimal? Discount { get; set; }

        /// <summary>Gets or sets the priced billing buckets for this model.</summary>
        public List<AIPriceEntry> Entries { get; set; } = new List<AIPriceEntry>();

        /// <summary>Returns the first entry billing <paramref name="kind"/>, or <c>null</c>.</summary>
        public AIPriceEntry Find(string kind)
            => this.Entries?.FirstOrDefault(e => e.Kind == kind);

        /// <summary>
        /// Gets the amount billed for <paramref name="kind"/>, or <c>null</c>
        /// when the model does not price that kind.
        /// </summary>
        public decimal? AmountOf(string kind)
            => this.Find(kind)?.Amount;

        /// <summary>
        /// Appends a token-priced entry — convenience for the unambiguous
        /// per-token kinds so seeds and tests can stay terse. Ambiguous
        /// modalities (audio, video, tools) must still declare their
        /// <see cref="AIPriceEntry"/> explicitly.
        /// </summary>
        private void AddTokenEntry(string kind, decimal? amount)
        {
            if (amount.HasValue)
            {
                this.Entries ??= new List<AIPriceEntry>();
                this.Entries.Add(new AIPriceEntry(kind, AIPriceUnit.Token, amount.Value));
            }
        }

        private void AddInvocationEntry(string kind, decimal? amount)
        {
            if (amount.HasValue)
            {
                this.Entries ??= new List<AIPriceEntry>();
                this.Entries.Add(new AIPriceEntry(kind, AIPriceUnit.Invocation, amount.Value));
            }
        }

        /// <summary>Gets or init-appends the per-token <c>text_input</c> price.</summary>
        public decimal? TextInput
        {
            get => this.AmountOf("text_input");
            init => this.AddTokenEntry("text_input", value);
        }

        /// <summary>Gets or init-appends the per-token <c>text_output</c> price.</summary>
        public decimal? TextOutput
        {
            get => this.AmountOf("text_output");
            init => this.AddTokenEntry("text_output", value);
        }

        /// <summary>Gets or init-appends the per-token <c>cache_read</c> price.</summary>
        public decimal? CacheRead
        {
            get => this.AmountOf("cache_read");
            init => this.AddTokenEntry("cache_read", value);
        }

        /// <summary>Gets or init-appends the per-token <c>cache_write</c> price.</summary>
        public decimal? CacheWrite
        {
            get => this.AmountOf("cache_write");
            init => this.AddTokenEntry("cache_write", value);
        }

        /// <summary>Gets or init-appends the per-token <c>internal_reasoning</c> price.</summary>
        public decimal? InternalReasoning
        {
            get => this.AmountOf("internal_reasoning");
            init => this.AddTokenEntry("internal_reasoning", value);
        }

        /// <summary>Gets or init-appends the per-token <c>image_input</c> price.</summary>
        public decimal? ImageInput
        {
            get => this.AmountOf("image_input");
            init => this.AddTokenEntry("image_input", value);
        }

        /// <summary>Gets or init-appends the per-token <c>image_output</c> price.</summary>
        public decimal? ImageOutput
        {
            get => this.AmountOf("image_output");
            init => this.AddTokenEntry("image_output", value);
        }

        /// <summary>Gets or init-appends the per-token <c>audio_input</c> price.</summary>
        public decimal? AudioInput
        {
            get => this.AmountOf("audio_input");
            init => this.AddTokenEntry("audio_input", value);
        }

        /// <summary>Gets or init-appends the per-token <c>audio_output</c> price.</summary>
        public decimal? AudioOutput
        {
            get => this.AmountOf("audio_output");
            init => this.AddTokenEntry("audio_output", value);
        }

        /// <summary>Gets or init-appends the per-token <c>audio_cache_read</c> price.</summary>
        public decimal? AudioCacheRead
        {
            get => this.AmountOf("audio_cache_read");
            init => this.AddTokenEntry("audio_cache_read", value);
        }

        /// <summary>Gets or init-appends the per-token <c>video_input</c> price.</summary>
        public decimal? VideoInput
        {
            get => this.AmountOf("video_input");
            init => this.AddTokenEntry("video_input", value);
        }

        /// <summary>Gets or init-appends the per-token <c>video_output</c> price.</summary>
        public decimal? VideoOutput
        {
            get => this.AmountOf("video_output");
            init => this.AddTokenEntry("video_output", value);
        }

        /// <summary>Gets or init-appends the per-invocation <c>request</c> price.</summary>
        public decimal? Request
        {
            get => this.AmountOf("request");
            init => this.AddInvocationEntry("request", value);
        }

        /// <summary>Gets or init-appends the per-invocation <c>tool:web_search</c> price.</summary>
        public decimal? WebSearch
        {
            get => this.AmountOf("tool:web_search");
            init => this.AddInvocationEntry("tool:web_search", value);
        }
    }
}
