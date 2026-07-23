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
using System.Drawing;
using Grasshopper.Kernel;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for synchronous input adapter components that convert various data types into AIInputPayload.
    /// Input adapters are simple, non-AI components that prepare data for AI processing.
    /// </summary>
    public abstract class AIInputAdapterBase : GH_Component
    {
        private readonly GH_Exposure _exposure;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIInputAdapterBase"/> class.
        /// </summary>
        /// <param name="name">Component name.</param>
        /// <param name="nickname">Component nickname.</param>
        /// <param name="description">Component description.</param>
        /// <param name="exposure">Component exposure level (primary or secondary).</param>
        protected AIInputAdapterBase(string name, string nickname, string description, GH_Exposure exposure)
            : base(name, nickname, description, "SmartHopper", "Input")
        {
            this._exposure = exposure;
        }

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected abstract Bitmap Icon { get; }

        /// <summary>
        /// Gets the component exposure level (primary or secondary).
        /// </summary>
        public override GH_Exposure Exposure => this._exposure;

        /// <summary>
        /// Creates an AIInputPayload from text content.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <param name="agent">The agent originating this payload (default: User).</param>
        /// <returns>An AIInputPayload wrapping the text.</returns>
        protected AIInputPayload CreateTextPayload(string content, AIAgent agent = AIAgent.User)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
            }

            return AIInputPayload.FromText(content, agent);
        }

        /// <summary>
        /// Creates an AIInputPayload from an image source.
        /// </summary>
        /// <param name="imageSource">The image source.</param>
        /// <param name="agent">The agent originating this payload (default: User).</param>
        /// <returns>An AIInputPayload wrapping the image.</returns>
        protected AIInputPayload CreateImagePayload(VersatileImage imageSource, AIAgent agent = AIAgent.User)
        {
            if (imageSource == null)
            {
                throw new ArgumentNullException(nameof(imageSource));
            }

            return AIInputPayload.FromImage(imageSource.ToInteraction(agent));
        }

        /// <summary>
        /// Creates an AIInputPayload from an audio interaction.
        /// </summary>
        /// <param name="interaction">The audio interaction.</param>
        /// <param name="agent">The agent originating this payload (default: User).</param>
        /// <returns>An AIInputPayload wrapping the audio.</returns>
        protected AIInputPayload CreateAudioPayload(AIInteractionAudio interaction, AIAgent agent = AIAgent.User)
        {
            if (interaction == null)
            {
                throw new ArgumentNullException(nameof(interaction));
            }

            interaction.Agent = agent;
            return AIInputPayload.FromAudio(interaction);
        }

        /// <summary>
        /// Creates an AIInputPayload from a context filter.
        /// </summary>
        /// <param name="contextFilter">The context filter string (e.g., "provider1,provider2" or "-provider1").</param>
        /// <returns>An AIInputPayload wrapping the context filter.</returns>
        protected AIInputPayload CreateContextPayload(string contextFilter)
        {
            if (string.IsNullOrWhiteSpace(contextFilter))
            {
                throw new ArgumentException("Context filter cannot be null or whitespace.", nameof(contextFilter));
            }

            return AIInputPayload.FromContextFilter(contextFilter);
        }

        /// <summary>
        /// Wraps an AIInputPayload in a GH_AIInputPayload for output.
        /// </summary>
        /// <param name="payload">The AIInputPayload to wrap.</param>
        /// <returns>A GH_AIInputPayload ready for output.</returns>
        protected GH_AIInputPayload WrapPayload(AIInputPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return new GH_AIInputPayload(payload);
        }

        /// <summary>
        /// Gets the tooltip description for the standard <c>Input &gt;</c> output.
        /// Override in subclasses to tailor the description (e.g. "AIInputPayload wrapping the audio file").
        /// </summary>
        protected virtual string PayloadOutputDescription => "AIInputPayload produced by this adapter.";

        /// <summary>
        /// Sealed registration of the standard <c>Input &gt;</c> output. Always present and at index 0.
        /// Subclasses must use <see cref="RegisterAdditionalOutputParams"/> to add extra outputs.
        /// </summary>
        protected sealed override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(
                new AIInputPayloadParameter(),
                "Input >",
                ">",
                this.PayloadOutputDescription,
                GH_ParamAccess.item);

            this.RegisterAdditionalOutputParams(pManager);
        }

        /// <summary>
        /// Hook for subclasses that need to expose extra outputs beyond the standard <c>Input &gt;</c> output.
        /// Default is a no-op. Indices for extra outputs start at 1.
        /// </summary>
        /// <param name="pManager">The Grasshopper output parameter manager.</param>
        protected virtual void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
        }
    }
}
