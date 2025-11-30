/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component for merging two GhJSON documents into one.
    /// Target document takes priority on conflicts.
    /// </summary>
    public class GhMergeComponents : GH_Component
    {
        private string lastMergedJson = string.Empty;
        private int lastComponentsAdded;
        private int lastConnectionsAdded;
        private int lastGroupsAdded;

        /// <summary>
        /// Initializes a new instance of the <see cref="GhMergeComponents"/> class.
        /// </summary>
        public GhMergeComponents()
            : base(
                "Merge GhJSON",
                "GhMerge",
                "Merge two GhJSON documents into one. Target takes priority on conflicts (duplicate components by GUID are skipped from source).",
                "SmartHopper",
                "Grasshopper")
        {
        }

        /// <summary>
        /// Gets the unique identifier for this component.
        /// </summary>
        public override Guid ComponentGuid => new Guid("A3C8F291-7D4E-4B5A-9E2F-8C1D6B3A5E7F");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Resources.ghmerge;

        /// <summary>
        /// Registers the input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Target", "T", "Target GhJSON document. Takes priority on conflicts.", GH_ParamAccess.item);
            pManager.AddTextParameter("Source", "S", "Source GhJSON document to merge into the target.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers the output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Merged", "M", "Merged GhJSON document", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Components+", "C+", "Number of components added from source", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Connections+", "W+", "Number of connections added from source", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Groups+", "G+", "Number of groups added from source", GH_ParamAccess.item);
        }

        /// <summary>
        /// Solves the component for the given data access.
        /// </summary>
        /// <param name="DA">The data access object for input/output operations.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Read "Run?" switch
            bool run = false;
            if (!DA.GetData(2, ref run))
            {
                return;
            }

            if (!run)
            {
                if (!string.IsNullOrEmpty(this.lastMergedJson))
                {
                    DA.SetData(0, this.lastMergedJson);
                    DA.SetData(1, this.lastComponentsAdded);
                    DA.SetData(2, this.lastConnectionsAdded);
                    DA.SetData(3, this.lastGroupsAdded);
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to merge documents");
                }

                return;
            }

            // 2. Clear previous results
            this.lastMergedJson = string.Empty;
            this.lastComponentsAdded = 0;
            this.lastConnectionsAdded = 0;
            this.lastGroupsAdded = 0;

            // 3. Get inputs
            string targetJson = null;
            string sourceJson = null;

            if (!DA.GetData(0, ref targetJson) || string.IsNullOrEmpty(targetJson))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Target GhJSON is required");
                return;
            }

            if (!DA.GetData(1, ref sourceJson) || string.IsNullOrEmpty(sourceJson))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Source GhJSON is required");
                return;
            }

            try
            {
                // 4. Call the AI tool
                var parameters = new JObject
                {
                    ["target"] = targetJson,
                    ["source"] = sourceJson,
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_merge",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_merge";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var aiResult = toolCall.Exec().GetAwaiter().GetResult();
                var toolResultInteraction = aiResult.Body.GetLastInteraction() as AIInteractionToolResult;
                var toolResult = toolResultInteraction?.Result;

                if (toolResult == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_merge' did not return a valid result");
                    return;
                }

                // 5. Extract results
                var mergedJson = toolResult["ghjson"]?.ToString() ?? string.Empty;
                var componentsAdded = toolResult["componentsAdded"]?.ToObject<int>() ?? 0;
                var componentsDuplicated = toolResult["componentsDuplicated"]?.ToObject<int>() ?? 0;
                var connectionsAdded = toolResult["connectionsAdded"]?.ToObject<int>() ?? 0;
                var connectionsDuplicated = toolResult["connectionsDuplicated"]?.ToObject<int>() ?? 0;
                var groupsAdded = toolResult["groupsAdded"]?.ToObject<int>() ?? 0;

                // Store for persistence
                this.lastMergedJson = mergedJson;
                this.lastComponentsAdded = componentsAdded;
                this.lastConnectionsAdded = connectionsAdded;
                this.lastGroupsAdded = groupsAdded;

                // Add info messages
                if (componentsDuplicated > 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{componentsDuplicated} duplicate component(s) skipped (target wins)");
                }

                if (connectionsDuplicated > 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{connectionsDuplicated} duplicate connection(s) skipped");
                }

                // 6. Set outputs
                DA.SetData(0, mergedJson);
                DA.SetData(1, componentsAdded);
                DA.SetData(2, connectionsAdded);
                DA.SetData(3, groupsAdded);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
