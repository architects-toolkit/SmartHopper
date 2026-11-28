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
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Fallback;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates text from AI input payloads.
    /// </summary>
    public class AI2TextComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2TextComponent"/> class.
        /// </summary>
        public AI2TextComponent()
            : base("AI to Text", "AI2Text", "Call the AI provider on the connected input(s) and return a plain-text response.", GH_Exposure.primary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("23F365BF-CA8A-40F5-9F76-0CC3841D9064");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.AI2Text;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are a helpful assistant. Generate clear, concise text responses based on the user input.";
        }

        /// <inheritdoc/>
        protected override IModalityFallback GetPassthroughConversion(AIBody mergedBody)
        {
            // Strict STT: every input item is audio — each maps to one transcript.
            var interactions = mergedBody?.Interactions;
            if (interactions == null || interactions.Count == 0)
            {
                return null;
            }

            return interactions.All(interaction => interaction is AIInteractionAudio)
                ? new AudioToTextFallback()
                : null;
        }

        /// <inheritdoc/>
        protected override IGH_Goo ConvertPassthroughItem(IAIInteraction item)
        {
            return item is AIInteractionText text && !string.IsNullOrWhiteSpace(text.Content)
                ? (IGH_Goo)new GH_String(text.Content.Trim())
                : null;
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
                    ParamName = "Text",
                    NickName = "T",
                    Description = "AI-generated text response.",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        if (aiReturn?.Body?.GetLastAssistantText() is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            // Trim leading and trailing whitespace before returning the text
                            var cleanedText = text.Trim();
                            return (IGH_Goo)new GH_String(cleanedText);
                        }

                        return null;
                    })
                }
            };
        }
    }
}
