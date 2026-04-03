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
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.Grasshopper.Types;
using SmartHopper.Infrastructure.AICall.Core;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Stateless component that assembles an <see cref="AIRequestParameters"/> from
    /// cross-provider universal inputs and an optional Extras JSON object from
    /// <see cref="AIExtraSettingsComponent"/>.
    /// </summary>
    public class AISettingsComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("133A3D6E-1DF0-4FD3-92B5-F686C2FD587D");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.settings;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "Settings",
            "AI Settings",
            "Parameters",
            "AI Parameters",
            "Request Settings",
            "Model Settings",
        };

        /// <summary>Initializes a new instance of <see cref="AISettingsComponent"/>.</summary>
        public AISettingsComponent()
            : base(
                "AI Settings",
                "AISett",
                "Assembles AI request settings (model, temperature, tokens, extras) to pass to any AI component.",
                "SmartHopper", "AI")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Model", "M", "AI model name override. Leave empty to use the provider default model.", GH_ParamAccess.item, string.Empty);
            pManager.AddIntegerParameter("Max Tokens", "Tok", "Maximum number of output tokens. Leave disconnected to use the global provider setting.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Temperature", "T", "Sampling temperature (0.0–2.0). Leave disconnected to use the global provider setting.\nSupported by OpenAI, Anthropic, MistralAI, DeepSeek.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Batch", "B", "When true, all AI calls in a single run are aggregated into one batch HTTP request (async, lower cost). Requires the active provider to support batch processing.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Extras", "X", "Provider-specific extra settings as a JSON object. Connect an AI Extra Settings component output here.", GH_ParamAccess.item, string.Empty);

            // All inputs are optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Settings", "S", "AI request settings. Connect to any AI component's Settings input.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var builder = AIRequestParameters.Create();

            string model = null;
            if (DA.GetData("Model", ref model) && !string.IsNullOrWhiteSpace(model))
            {
                builder.WithModel(model.Trim());
            }

            double temperature = double.NaN;
            if (DA.GetData("Temperature", ref temperature) && !double.IsNaN(temperature))
            {
                builder.WithTemperature(temperature);
            }

            int maxTokens = 0;
            if (DA.GetData("Max Tokens", ref maxTokens) && maxTokens > 0)
            {
                builder.WithMaxTokens(maxTokens);
            }

            string extrasJson = null;
            if (DA.GetData("Extras", ref extrasJson) && !string.IsNullOrWhiteSpace(extrasJson))
            {
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(extrasJson);
                    if (dict != null)
                    {
                        builder.WithExtras(new ReadOnlyDictionary<string, JToken>(dict));
                    }
                }
                catch (Exception ex)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Extras JSON is invalid and will be ignored: {ex.Message}");
                }
            }

            bool batch = false;
            if (DA.GetData("Batch", ref batch) && batch)
            {
                builder.WithBatchTier(true);
            }

            DA.SetData("Settings", new GH_AIRequestParameters(builder.Build()));
        }
    }
}
