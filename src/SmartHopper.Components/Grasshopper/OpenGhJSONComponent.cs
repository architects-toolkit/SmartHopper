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
using GhJSON.Core.Validation;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Opens a .ghjson file from disk and returns its content with validation output.
    /// </summary>
    public class OpenGhJSON : StatefulComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenGhJSON"/> class.
        /// </summary>
        public OpenGhJSON()
            : base(
                  "Open GhJSON",
                  "OpenGhJSON",
                  "Open a .ghjson file from disk and return its content with validation output. Input files must have extension .ghjson.",
                  "SmartHopper",
                  "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("D2FF1BE9-C365-4F6A-96CC-959BF252768A");

        /// <inheritdoc/>
        protected override System.Drawing.Bitmap Icon => null;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Path", "P", "Absolute file path to a .ghjson file.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("GhJSON", "G", "GhJSON document content.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Valid", "V", "Whether the GhJSON document is valid.", GH_ParamAccess.item);
            pManager.AddTextParameter("Errors", "E", "List of validation errors.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new OpenGhJSONWorker(this, this.AddRuntimeMessage);
        }

        private sealed class OpenGhJSONWorker : AsyncWorkerBase
        {
            private readonly OpenGhJSON parent;
            private string filePath;
            private bool hasWork;
            private string ghJson;
            private bool isValid;
            private List<string> errors;

            public OpenGhJSONWorker(
                OpenGhJSON parent,
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
                this.ghJson = null;
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

                    // Validate extension is .ghjson
                    var extension = Path.GetExtension(path);
                    if (!string.Equals(extension, ".ghjson", StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Input file must have .ghjson extension. Found: '{extension}'.");
                        return;
                    }

                    // Validate file exists
                    if (!File.Exists(path))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"File not found: '{path}'.");
                        return;
                    }

                    // Read file content
                    this.ghJson = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(this.ghJson))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "File is empty.");
                        return;
                    }

                    token.ThrowIfCancellationRequested();

                    // Validate GhJSON
                    var result = await GhJson.ValidateAsync(this.ghJson, ValidationLevel.Standard, schemaVersion: null, preferOnline: false, token).ConfigureAwait(false);

                    this.isValid = result.IsValid;
                    this.errors = result.Errors?.Select(e => e.ToString()).ToList() ?? new List<string>();

                    if (!this.isValid)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"GhJSON validation failed with {this.errors.Count} error(s): {string.Join("; ", this.errors)}");
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
                if (!string.IsNullOrWhiteSpace(this.ghJson))
                {
                    this.parent.SetPersistentOutput("GhJSON", this.ghJson, DA);
                    this.parent.SetPersistentOutput("Valid", this.isValid, DA);
                    this.parent.SetPersistentOutput("Errors", this.errors, DA);
                    message = this.isValid ? "Valid" : "Invalid";
                }
                else
                {
                    this.parent.SetPersistentOutput("GhJSON", string.Empty, DA);
                    this.parent.SetPersistentOutput("Valid", false, DA);
                    this.parent.SetPersistentOutput("Errors", new List<string>(), DA);
                    message = "Not loaded";
                }
            }
        }
    }
}
