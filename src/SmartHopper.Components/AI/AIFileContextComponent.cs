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
using System.Collections.Generic;
using Grasshopper.Kernel;
using SmartHopper.Core.AI;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Component that provides context for AI queries.
    /// </summary>
    public class AIFileContextComponent : GH_Component, IAIContextProvider
    {
        private string context;

        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        public string ProviderId => "file";

        /// <summary>
        /// Initializes a new instance of the <see cref="AIFileContextComponent"/> class.
        /// Constructor for the AI File Context component.
        /// </summary>
        public AIFileContextComponent()
            : base("AI File Context", "AIFileCtx",
                "Defines the current file context for AI-powered components",
                "SmartHopper", "AI")
        {
            // Register this component as a context provider
            AIContextManager.RegisterProvider(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Context", "C", "Describe which is the aim of this file, which are your expectations of the results, which are the main input parameters, and what to avoid", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // No outputs needed
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            this.context = string.Empty;

            if (!DA.GetData(0, ref this.context))
            {
                return;
            }
        }

        public Dictionary<string, string> GetContext()
        {
            return new Dictionary<string, string> { { "file-context", this.context } };
        }

        public override Guid ComponentGuid => new ("A7F5D347-9F4E-4A75-B6A9-115C06B6115D");
    }
}
