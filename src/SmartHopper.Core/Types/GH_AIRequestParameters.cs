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
using System.Collections.ObjectModel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Grasshopper wrapper for <see cref="AIRequestParameters"/>.
    /// Supports cast from plain string (model name) for backwards compatibility.
    /// </summary>
    public class GH_AIRequestParameters : GH_Goo<AIRequestParameters>
    {
        /// <summary>Initializes a new empty instance.</summary>
        public GH_AIRequestParameters()
            : base(AIRequestParameters.Empty)
        {
        }

        /// <summary>Initializes a new instance wrapping the given parameters.</summary>
        public GH_AIRequestParameters(AIRequestParameters parameters)
            : base(parameters ?? AIRequestParameters.Empty)
        {
        }

        /// <inheritdoc/>
        public override bool IsValid => this.Value != null;

        /// <inheritdoc/>
        public override string IsValidWhyNot => this.IsValid ? string.Empty : "Value is null";

        /// <inheritdoc/>
        public override string TypeName => "AI Settings";

        /// <inheritdoc/>
        public override string TypeDescription => "AI request parameters including model, temperature, max tokens, and provider-specific extras.";

        /// <inheritdoc/>
        public override IGH_Goo Duplicate() => new GH_AIRequestParameters(this.Value);

        /// <inheritdoc/>
        public override string ToString()
        {
            var v = this.Value;
            if (v == null) return "AI Settings (empty)";
            var parts = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(v.Model)) parts.Append($"Model={v.Model}");

            if (v.MaxTokens.HasValue)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"Tok={v.MaxTokens}");
            }

            if (v.Temperature.HasValue)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"Temp={v.Temperature}");
            }

            if (v.BatchTier)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append("Batch");
            }

            if (v.TimeoutSeconds.HasValue)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"Time={v.TimeoutSeconds}s");
            }

            if (v.Extras != null && v.Extras.Count > 0)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"Extras({v.Extras.Count})");
            }

            return parts.Length > 0 ? $"AI Settings [{parts}]" : "AI Settings (defaults)";
        }

        /// <inheritdoc/>
        public override bool CastFrom(object source)
        {
            if (source == null) return false;

            // Accept another GH_AIRequestParameters
            if (source is GH_AIRequestParameters ghParams)
            {
                this.Value = ghParams.Value;
                return true;
            }

            // Accept raw AIRequestParameters
            if (source is AIRequestParameters raw)
            {
                this.Value = raw;
                return true;
            }

            // Accept string → treat as model name (backwards compatibility)
            string text = null;
            if (source is GH_String ghStr)
            {
                text = ghStr.Value;
            }
            else if (source is string s)
            {
                text = s;
            }

            if (text != null)
            {
                this.Value = AIRequestParameters.FromModel(text.Trim());
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q) == typeof(GH_AIRequestParameters))
            {
                target = (Q)(object)new GH_AIRequestParameters(this.Value);
                return true;
            }

            if (typeof(Q) == typeof(AIRequestParameters))
            {
                target = (Q)(object)this.Value;
                return true;
            }

            // Allow casting back to GH_String as model name (single-field round-trip)
            if (typeof(Q) == typeof(GH_String))
            {
                target = (Q)(object)new GH_String(this.Value?.Model ?? string.Empty);
                return true;
            }

            return false;
        }

        /// <summary>Serializes to GH file.</summary>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            try
            {
                var v = this.Value;

                if (v == null) return true;

                if (!string.IsNullOrEmpty(v.Model)) writer.SetString("Model", v.Model);

                if (v.Temperature.HasValue) writer.SetDouble("Temperature", v.Temperature.Value);

                if (v.MaxTokens.HasValue) writer.SetInt32("MaxTokens", v.MaxTokens.Value);

                if (v.TopP.HasValue) writer.SetDouble("TopP", v.TopP.Value);

                if (v.Seed.HasValue) writer.SetInt32("Seed", v.Seed.Value);

                if (v.Extras != null && v.Extras.Count > 0)
                {
                    writer.SetString("Extras", JsonConvert.SerializeObject(v.Extras));
                }

                if (v.BatchTier) writer.SetBoolean("BatchTier", v.BatchTier);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Deserializes from GH file.</summary>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            try
            {
                var builder = AIRequestParameters.Create();
                if (reader.ItemExists("Model")) builder.WithModel(reader.GetString("Model"));
                if (reader.ItemExists("Temperature")) builder.WithTemperature(reader.GetDouble("Temperature"));
                if (reader.ItemExists("MaxTokens")) builder.WithMaxTokens(reader.GetInt32("MaxTokens"));
                if (reader.ItemExists("TopP")) builder.WithTopP(reader.GetDouble("TopP"));
                if (reader.ItemExists("Seed")) builder.WithSeed(reader.GetInt32("Seed"));
                if (reader.ItemExists("Extras"))
                {
                    var extrasJson = reader.GetString("Extras");
                    if (!string.IsNullOrEmpty(extrasJson))
                    {
                        var dict = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(extrasJson);
                        if (dict != null)
                        {
                            builder.WithExtras(new ReadOnlyDictionary<string, JToken>(dict));
                        }
                    }
                }

                if (reader.ItemExists("BatchTier")) builder.WithBatchTier(reader.GetBoolean("BatchTier"));

                this.Value = builder.Build();
                return true;
            }
            catch
            {
                this.Value = AIRequestParameters.Empty;
                return false;
            }
        }
    }
}
