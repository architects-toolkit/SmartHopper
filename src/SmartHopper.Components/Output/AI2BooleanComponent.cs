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
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Classifies input as true or false using AI.
    /// </summary>
    public class AI2BooleanComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2BooleanComponent"/> class.
        /// </summary>
        public AI2BooleanComponent()
            : base("AI to Boolean", "AI→Bool", "Classify input as true or false", GH_Exposure.primary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("2742C0E6-0195-4A6E-9F89-1B9CBA4A3D87");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text_generate" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are a classification assistant. Respond with only 'true' or 'false' based on the user input. No other text.";
        }

        /// <summary>
        /// Gets the output mappings.
        /// </summary>
        protected override IReadOnlyList<OutputMapping> GetOutputMappings()
        {
            return new[]
            {
                new OutputMapping
                {
                    ParamName = "Boolean",
                    NickName = "B",
                    Description = "Boolean classification result",
                    ParamType = typeof(Param_Boolean),
                    Access = GH_ParamAccess.tree,
                    Extractor = (aiReturn) =>
                    {
                        if (aiReturn?.Body?.GetLastAssistantText() is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            // Use AIResponseParser to extract boolean value
                            var parsedBool = AIResponseParser.ParseBooleanFromResponse(text);
                            if (parsedBool.HasValue)
                            {
                                return new GH_Boolean(parsedBool.Value);
                            }
                        }

                        return null;
                    }
                }
            };
        }
    }
}
