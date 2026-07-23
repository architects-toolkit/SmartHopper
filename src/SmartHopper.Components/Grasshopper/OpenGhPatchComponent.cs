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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Opens a .ghpatch file from disk and returns its content with validation output.
    /// </summary>
    public class OpenGhPatch : StatefulComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenGhPatch"/> class.
        /// </summary>
        public OpenGhPatch()
            : base(
                  "Open GhPatch",
                  "OpenGhPatch",
                  "Open a .ghpatch file from disk and return its content with validation output. Input files must have extension .ghpatch.",
                  "SmartHopper",
                  "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("56AF5EE4-3847-49CD-A3D1-F1E12BF1C0B0");

        /// <inheritdoc/>
        protected override System.Drawing.Bitmap Icon => null;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Path", "P", "Absolute file path to a .ghpatch file.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("GhPatch", "P", "GhPatch document content.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Valid", "V", "Whether the GhPatch document is valid.", GH_ParamAccess.item);
            pManager.AddTextParameter("Errors", "E", "List of validation errors.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new OpenGhPatchWorker(this, this.AddRuntimeMessage);
        }

        private sealed class OpenGhPatchWorker : AsyncWorkerBase
        {
            private readonly OpenGhPatch parent;
            private string filePath;
            private bool hasWork;
            private string ghPatch;
            private bool isValid;
            private List<string> errors;

            public OpenGhPatchWorker(
                OpenGhPatch parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.errors = new List<string>();
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.filePath = null;
                DA.GetData(0, ref this.filePath);

                this.hasWork = !string.IsNullOrWhiteSpace(this.filePath);
                dataCount = this.hasWork ? 1 : 0;

                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Path input is empty.");
                }
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.ghPatch = null;
                this.isValid = false;
                this.errors = new List<string>();

                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    token.ThrowIfCancellationRequested();

                    var path = this.filePath.Trim();

                    // Validate extension is .ghpatch
                    var extension = Path.GetExtension(path);
                    if (!string.Equals(extension, ".ghpatch", StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Input file must have .ghpatch extension. Found: '{extension}'.");
                        return;
                    }

                    // Validate file exists
                    if (!File.Exists(path))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"File not found: '{path}'.");
                        return;
                    }

                    // Read file content
                    this.ghPatch = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(this.ghPatch))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "File is empty.");
                        return;
                    }

                    token.ThrowIfCancellationRequested();

                    // Validate GhPatch (synchronous API)
                    var result = GhJson.ValidatePatch(this.ghPatch, preferOnline: false, schemaVersion: null);

                    this.isValid = result.IsValid;
                    this.errors = result.Errors?.Select(e => e.ToString()).ToList() ?? new List<string>();

                    if (!this.isValid)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"GhPatch validation failed with {this.errors.Count} error(s): {string.Join("; ", this.errors)}");
                    }
                }
                catch (OperationCanceledException)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (!string.IsNullOrWhiteSpace(this.ghPatch))
                {
                    this.parent.SetPersistentOutput("GhPatch", this.ghPatch, DA);
                    this.parent.SetPersistentOutput("Valid", this.isValid, DA);
                    this.parent.SetPersistentOutput("Errors", this.errors, DA);
                    message = this.isValid ? "Valid" : "Invalid";
                }
                else
                {
                    this.parent.SetPersistentOutput("GhPatch", string.Empty, DA);
                    this.parent.SetPersistentOutput("Valid", false, DA);
                    this.parent.SetPersistentOutput("Errors", new List<string>(), DA);
                    message = "Not loaded";
                }
            }
        }
    }
}
