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
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Models;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Grasshopper goo wrapper for AIInputPayload.
    /// Provides serialization, casting, and UI rendering support.
    /// </summary>
    public class GH_AIInputPayload : GH_Goo<AIInputPayload>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GH_AIInputPayload"/> class.
        /// </summary>
        public GH_AIInputPayload()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GH_AIInputPayload"/> class with a payload.
        /// </summary>
        /// <param name="payload">The AIInputPayload to wrap.</param>
        public GH_AIInputPayload(AIInputPayload payload)
            : base(payload)
        {
        }

        /// <summary>
        /// Gets a value indicating whether the payload is valid.
        /// </summary>
        public override bool IsValid => this.Value != null && this.Value.Interactions != null;

        /// <summary>
        /// Gets the type name for display.
        /// </summary>
        public override string TypeName => "AIInputPayload";

        /// <summary>
        /// Gets the type description.
        /// </summary>
        public override string TypeDescription => "A payload of AI interactions for wiring between components";

        /// <summary>
        /// Duplicates this goo.
        /// </summary>
        /// <returns>A duplicate of this goo.</returns>
        public override IGH_Goo Duplicate()
        {
            if (this.Value == null)
            {
                return new GH_AIInputPayload();
            }

            var newPayload = new AIInputPayload(
                new System.Collections.Generic.List<IAIInteraction>(this.Value.Interactions),
                this.Value.InputCapabilityAtSource,
                this.Value.PayloadType,
                this.Value.Hint);

            return new GH_AIInputPayload(newPayload);
        }

        /// <summary>
        /// Gets a string representation of this goo.
        /// </summary>
        /// <returns>A string representation.</returns>
        public override string ToString()
        {
            if (this.Value == null)
            {
                return "Null AIInputPayload";
            }

            var interactionCount = this.Value.Interactions?.Count ?? 0;
            return $"AIInputPayload ({this.Value.PayloadType}, {interactionCount} interaction{(interactionCount != 1 ? "s" : string.Empty)})";
        }

        /// <summary>
        /// Attempts to cast from another goo type.
        /// </summary>
        /// <param name="source">The source goo.</param>
        /// <returns>True if cast succeeded; otherwise false.</returns>
        public override bool CastFrom(object source)
        {
            if (source is GH_AIInputPayload payload)
            {
                this.Value = payload.Value;
                return true;
            }

            if (source is AIInputPayload directPayload)
            {
                this.Value = directPayload;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to cast to another goo type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="target">The target goo (output).</param>
        /// <returns>True if cast succeeded; otherwise false.</returns>
        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T) == typeof(GH_AIInputPayload))
            {
                target = (T)(object)new GH_AIInputPayload(this.Value);
                return true;
            }

            if (typeof(T) == typeof(AIInputPayload))
            {
                target = (T)(object)this.Value;
                return true;
            }

            return false;
        }
    }
}
