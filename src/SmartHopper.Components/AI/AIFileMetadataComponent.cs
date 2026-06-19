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
using SmartHopper.Components.Properties;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Component that provides file metadata for AI queries and GhJSON generation.
    /// </summary>
    public class AIFileMetadataComponent : GH_Component, IAIContextProvider
    {
        private string title;
        private string description;
        private string version;
        private string author;
        private List<string> tags = new List<string>();

        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        public string ProviderId => "file-metadata";

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Resources.context;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "File Metadata",
            "Metadata",
            "Title",
            "Description",
            "Author",
            "Tags",
            "AI Metadata",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIFileMetadataComponent"/> class.
        /// Constructor for the AI File Metadata component.
        /// </summary>
        public AIFileMetadataComponent()
            : base("File Metadata", "FileMeta",
                "Defines metadata for the current file (title, description, version, author, tags).\nAI-powered components will read this information to generate relevant responses, and it will be included in GhJSON when 'Include Metadata' is enabled.",
                "SmartHopper",
                "A. AI")
        {
            // Register this component as a context provider
            AIContextManager.RegisterProvider(this);
        }

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            AIContextManager.UnregisterProvider(this);
            base.RemovedFromDocument(document);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Title", "T", "Title of the file or definition", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Description", "D", "Description of what this file does, your expectations of the results, the main input parameters, and what to avoid (also used as metadata for GhJSON)", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Version", "V", "Version of the definition", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Author", "A", "Author of the definition", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Tags", "Ta", "Tags for categorizing and searching definitions", GH_ParamAccess.list, string.Empty);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // No outputs needed
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            this.title = string.Empty;
            this.description = string.Empty;
            this.version = string.Empty;
            this.author = string.Empty;
            this.tags = new List<string>();

            DA.GetData(0, ref this.title);
            DA.GetData(1, ref this.description);
            DA.GetData(2, ref this.version);
            DA.GetData(3, ref this.author);
            DA.GetDataList(4, this.tags);
        }

        public Dictionary<string, string> GetContext()
        {
            var context = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(this.title))
            {
                context["title"] = this.title;
            }

            if (!string.IsNullOrWhiteSpace(this.description))
            {
                context["description"] = this.description;
            }

            if (!string.IsNullOrWhiteSpace(this.version))
            {
                context["version"] = this.version;
            }

            if (!string.IsNullOrWhiteSpace(this.author))
            {
                context["author"] = this.author;
            }

            if (this.tags != null && this.tags.Count > 0)
            {
                var nonEmptyTags = this.tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                if (nonEmptyTags.Count > 0)
                {
                    context["tags"] = string.Join(", ", nonEmptyTags);
                }
            }

            return context;
        }

        public override Guid ComponentGuid => new ("16B249AE-DC5A-4FCC-AA4A-C4D3698CE468");
    }
}
