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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Saves a GhPatch string to a .ghpatch file on disk.
    /// </summary>
    public class SaveGhPatch : StatefulComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SaveGhPatch"/> class.
        /// </summary>
        public SaveGhPatch()
            : base(
                  "Save GhPatch",
                  "SaveGhPatch",
                  "Save a GhPatch string to a .ghpatch file. If the path includes an extension, it will be replaced with .ghpatch.",
                  "SmartHopper",
                  "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("B76708B2-C0CA-41B1-A437-885BFF5FE4E2");

        /// <inheritdoc/>
        protected override System.Drawing.Bitmap Icon => null;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("GhPatch", "P", "GhPatch document string to save.", GH_ParamAccess.item);
            pManager.AddTextParameter("Path", "P", "Absolute file path. If an extension is present, it will be replaced with .ghpatch.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Saved Path", "S", "The actual path where the file was saved.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new SaveGhPatchWorker(this, this.AddRuntimeMessage);
        }

        private sealed class SaveGhPatchWorker : AsyncWorkerBase
        {
            private readonly SaveGhPatch parent;
            private string ghPatch;
            private string filePath;
            private bool hasWork;
            private string savedPath;

            public SaveGhPatchWorker(
                SaveGhPatch parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.ghPatch = null;
                DA.GetData(0, ref this.ghPatch);

                this.filePath = null;
                DA.GetData(1, ref this.filePath);

                this.hasWork = !string.IsNullOrWhiteSpace(this.ghPatch) && !string.IsNullOrWhiteSpace(this.filePath);
                dataCount = this.hasWork ? 1 : 0;

                if (!this.hasWork)
                {
                    if (string.IsNullOrWhiteSpace(this.ghPatch))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "GhPatch input is empty.");
                    }

                    if (string.IsNullOrWhiteSpace(this.filePath))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Path input is empty.");
                    }
                }
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.savedPath = null;

                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    token.ThrowIfCancellationRequested();

                    var path = this.filePath.Trim();

                    // Validate that the path includes a file name
                    var fileName = Path.GetFileName(path);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "The path must include a file name.");
                        return;
                    }

                    // If the path has an extension, remove it and use .ghpatch
                    var extension = Path.GetExtension(path);
                    if (!string.IsNullOrEmpty(extension))
                    {
                        path = path.Substring(0, path.Length - extension.Length);
                    }

                    path += ".ghpatch";

                    // Ensure the directory exists
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    await File.WriteAllTextAsync(path, this.ghPatch, token).ConfigureAwait(false);

                    this.savedPath = path;
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
                if (!string.IsNullOrWhiteSpace(this.savedPath))
                {
                    this.parent.SetPersistentOutput("Saved Path", this.savedPath, DA);
                    message = "Saved";
                }
                else
                {
                    this.parent.SetPersistentOutput("Saved Path", string.Empty, DA);
                    message = "Not saved";
                }
            }
        }
    }
}
