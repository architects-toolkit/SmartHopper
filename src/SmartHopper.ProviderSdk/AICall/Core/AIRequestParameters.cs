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
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace SmartHopper.ProviderSdk.AICall.Core
{
    /// <summary>
    /// Immutable per-request AI configuration. Providers read each property individually
    /// with fallback precedence: AIRequestParameters → global provider settings → provider defaults.
    /// Provider-specific settings (e.g. reasoning_effort) are passed via <see cref="Extras"/>.
    /// </summary>
    public sealed record AIRequestParameters
    {
        /// <summary>Gets the model override. Null means "use provider default".</summary>
        public string Model { get; init; }

        /// <summary>Gets the temperature override (0.0–2.0). Null means "use global setting".</summary>
        public double? Temperature { get; init; }

        /// <summary>Gets the max tokens override. Null means "use global setting".</summary>
        public int? MaxTokens { get; init; }

        /// <summary>Gets the timeout override in seconds. Null means "resolve from settings based on operation type".</summary>
        public int? TimeoutSeconds { get; init; }

        /// <summary>Gets the top-p override (0.0–1.0). Null means omit.</summary>
        public double? TopP { get; init; }

        /// <summary>Gets the seed for reproducibility. Null means omit.</summary>
        public int? Seed { get; init; }

        /// <summary>
        /// Gets provider-specific extra parameters as key/value pairs serialized to JSON.
        /// Well-known keys: "reasoning_effort" (OpenAI o-series/gpt-5), "safe_prompt" (MistralAI),
        /// "top_k" (Anthropic), "allow_fallback" (OpenRouter), "sort" (OpenRouter),
        /// "presence_penalty"/"frequency_penalty" (OpenAI, DeepSeek),
        /// "parallel_tool_calls" (OpenAI).
        /// </summary>
        public IReadOnlyDictionary<string, JToken> Extras { get; init; }

        /// <summary>
        /// Gets a value indicating whether to use asynchronous batch processing.
        /// When <c>true</c>, all tool calls in a single component run are aggregated into
        /// one batch HTTP request and submitted via <c>IAIBatchProvider</c>.
        /// Only effective if the active provider implements <c>IAIBatchProvider</c>.
        /// </summary>
        public bool BatchTier { get; init; }

        /// <summary>Gets an empty (default) instance with no overrides.</summary>
        public static AIRequestParameters Empty { get; } = new AIRequestParameters();

        /// <summary>Creates a new builder for constructing <see cref="AIRequestParameters"/>.</summary>
        public static AIRequestParametersBuilder Create() => new AIRequestParametersBuilder();

        /// <summary>
        /// Convenience: creates parameters with just a model name.
        /// Useful for backwards-compatible single-string wires.
        /// </summary>
        public static AIRequestParameters FromModel(string model) =>
            string.IsNullOrWhiteSpace(model) ? Empty : new AIRequestParameters { Model = model };
    }

    /// <summary>
    /// Fluent builder for <see cref="AIRequestParameters"/>.
    /// </summary>
    public sealed class AIRequestParametersBuilder
    {
        private string _model;
        private double? _temperature;
        private int? _maxTokens;
        private int? _timeoutSeconds;
        private double? _topP;
        private int? _seed;
        private bool _batchTier;
        private readonly Dictionary<string, JToken> _extras = new Dictionary<string, JToken>();

        /// <inheritdoc cref="AIRequestParameters.Model"/>
        public AIRequestParametersBuilder WithModel(string model)
        {
            this._model = model;
            return this;
        }

        /// <inheritdoc cref="AIRequestParameters.Temperature"/>
        public AIRequestParametersBuilder WithTemperature(double? temperature)
        {
            this._temperature = temperature;
            return this;
        }

        /// <inheritdoc cref="AIRequestParameters.MaxTokens"/>
        public AIRequestParametersBuilder WithMaxTokens(int? maxTokens)
        {
            this._maxTokens = maxTokens;
            return this;
        }

        /// <inheritdoc cref="AIRequestParameters.TimeoutSeconds"/>
        public AIRequestParametersBuilder WithTimeout(int? timeoutSeconds)
        {
            this._timeoutSeconds = timeoutSeconds;
            return this;
        }

        /// <inheritdoc cref="AIRequestParameters.TopP"/>
        public AIRequestParametersBuilder WithTopP(double? topP)
        {
            this._topP = topP;
            return this;
        }

        /// <inheritdoc cref="AIRequestParameters.Seed"/>
        public AIRequestParametersBuilder WithSeed(int? seed)
        {
            this._seed = seed;
            return this;
        }

        /// <inheritdoc cref="AIRequestParameters.BatchTier"/>
        public AIRequestParametersBuilder WithBatchTier(bool batchTier)
        {
            this._batchTier = batchTier;
            return this;
        }

        /// <summary>Adds or overwrites a single extra parameter.</summary>
        public AIRequestParametersBuilder WithExtra(string key, JToken value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                this._extras[key] = value;
            }

            return this;
        }

        /// <summary>Merges a dictionary of extra parameters (last write wins).</summary>
        public AIRequestParametersBuilder WithExtras(IReadOnlyDictionary<string, JToken> extras)
        {
            if (extras != null)
            {
                foreach (var kv in extras)
                {
                    this._extras[kv.Key] = kv.Value;
                }
            }

            return this;
        }

        /// <summary>Adds or overwrites a single extra parameter (alias for WithExtra).</summary>
        public AIRequestParametersBuilder AddExtra(string key, JToken value) => this.WithExtra(key, value);

        /// <summary>Merges a dictionary of extra parameters (alias for WithExtras).</summary>
        public AIRequestParametersBuilder AddExtras(IReadOnlyDictionary<string, JToken> extras) => this.WithExtras(extras);

        /// <summary>Removes an extra parameter by key.</summary>
        public AIRequestParametersBuilder RemoveExtra(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                this._extras.Remove(key);
            }

            return this;
        }

        /// <summary>Clears the model override.</summary>
        public AIRequestParametersBuilder ClearModel()
        {
            this._model = null;
            return this;
        }

        /// <summary>Clears the temperature override.</summary>
        public AIRequestParametersBuilder ClearTemperature()
        {
            this._temperature = null;
            return this;
        }

        /// <summary>Clears the max tokens override.</summary>
        public AIRequestParametersBuilder ClearMaxTokens()
        {
            this._maxTokens = null;
            return this;
        }

        /// <summary>Clears the timeout override.</summary>
        public AIRequestParametersBuilder ClearTimeout()
        {
            this._timeoutSeconds = null;
            return this;
        }

        /// <summary>Clears the top-p override.</summary>
        public AIRequestParametersBuilder ClearTopP()
        {
            this._topP = null;
            return this;
        }

        /// <summary>Clears the seed override.</summary>
        public AIRequestParametersBuilder ClearSeed()
        {
            this._seed = null;
            return this;
        }

        /// <summary>Resets the batch tier flag to false.</summary>
        public AIRequestParametersBuilder ClearBatchTier()
        {
            this._batchTier = false;
            return this;
        }

        /// <summary>Clears all extra parameters.</summary>
        public AIRequestParametersBuilder ClearExtras()
        {
            this._extras.Clear();
            return this;
        }

        /// <summary>Builds and returns the immutable <see cref="AIRequestParameters"/>.</summary>
        public AIRequestParameters Build() => new AIRequestParameters
        {
            Model = this._model,
            Temperature = this._temperature,
            MaxTokens = this._maxTokens,
            TimeoutSeconds = this._timeoutSeconds,
            TopP = this._topP,
            Seed = this._seed,
            BatchTier = this._batchTier,
            Extras = new ReadOnlyDictionary<string, JToken>(this._extras),
        };
    }
}
