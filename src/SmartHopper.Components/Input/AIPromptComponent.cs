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
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Wraps prompt text into an AIInputPayload with System agent role.
    /// Used for system prompts that guide AI behavior.
    /// </summary>
    public class AIPromptComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("5CD4D50A-35FB-45CC-91AB-CFFA0B5F1DD2");

        protected override Bitmap Icon => Resources.aiprompt;

        public AIPromptComponent()
            : base("AI Prompt", "AIPrompt", "Wraps prompt text into an AIInputPayload with System role for guiding AI behavior.", GH_Exposure.primary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "System prompt to guide AI behavior.", GH_ParamAccess.item);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the prompt with System role.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string prompt = null;
            if (!DA.GetData(0, ref prompt))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Prompt cannot be empty.");
                return;
            }

            try
            {
                var payload = this.CreateTextPayload(prompt, AIAgent.System);
                DA.SetData(0, this.WrapPayload(payload));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error creating payload: {ex.Message}");
            }
        }
    }
}
