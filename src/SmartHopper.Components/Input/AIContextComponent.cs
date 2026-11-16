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
using Grasshopper.Kernel;
using SmartHopper.Components.Properties;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Input adapter component that retrieves context from registered context providers
    /// and outputs it as a GH_AIInputPayload for wiring to output components.
    /// </summary>
    public class AIContextComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIContextComponent"/> class.
        /// </summary>
        public AIContextComponent()
            : base("AI Context", "AICtx", "Include live environment or document context (such as 'time', 'file', 'environment', 'selection', or 'viewport') in an AI request.", "SmartHopper", "B. Input")
        {
        }

        /// <summary>
        /// Gets the unique ID for this component.
        /// </summary>
        public override Guid ComponentGuid => new Guid("65B447B0-6DE7-48C5-97EA-52F6734FFB0A");

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.obscure;

        /// <summary>
        /// Gets the icon for this component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.AIContext;

        /// <summary>
        /// Registers input parameters.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Provider ID", "ID", "Context provider ID to include (e.g., 'time', 'file', 'environment', 'selection', 'viewport'). Use the Context Providers component to list all available IDs.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers output parameters.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "Packaged context input ready to wire into any AI output component's 'Input >' parameter.", GH_ParamAccess.item);
            pManager.AddTextParameter("Display", "D", "Human-readable preview of the context key-value pairs.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Solves the component.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string providerId = null;
            if (!DA.GetData(0, ref providerId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(providerId))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Provider ID cannot be empty.");
                return;
            }

            try
            {
                // Retrieve context from the specified provider
                var context = AIContextManager.GetCurrentContext(providerId);

                if (context == null || context.Count == 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"No context data found for provider '{providerId}'.");
                    return;
                }

                // Create a context payload
                var payload = AIInputPayload.FromContextFilter(providerId);

                // Format context for display
                var displayLines = new List<string>();
                foreach (var kvp in context)
                {
                    displayLines.Add($"{kvp.Key}: {kvp.Value}");
                }

                var displayText = string.Join("\n", displayLines);

                // Output the payload and display text
                DA.SetData(0, new GH_AIInputPayload(payload));
                DA.SetData(1, displayText);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error retrieving context: {ex.Message}");
            }
        }
    }
}
