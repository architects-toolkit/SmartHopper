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
using GhJSON.Core;
using GhJSON.Core.Validation;
using Grasshopper.Kernel;

using SmartHopper.Components.Properties;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component that validates a GhJSON document against the official schema
    /// and performs structural checks.
    /// </summary>
    public class GhValidateComponents : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhValidateComponents"/> class.
        /// </summary>
        public GhValidateComponents()
            : base(
                "Validate GhJSON",
                "GhValidate",
                "Validates a GhJSON document against the official schema and performs structural checks.",
                "SmartHopper",
                "Grasshopper")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("BBF48853-AC02-4786-B6A1-B74333DD1642");

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        protected override Bitmap Icon => null;

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("GhJSON", "J", "GhJSON document string to validate.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Level", "L", "Validation level: 0 = Minimal, 1 = Standard, 2 = Strict. Defaults to Standard.", GH_ParamAccess.item, 1);
            pManager.AddBooleanParameter("Prefer Online", "O", "Prefer downloading the schema from the official online repository.", GH_ParamAccess.item, false);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Valid", "V", "Whether the GhJSON document is valid.", GH_ParamAccess.item);
            pManager.AddTextParameter("Errors", "E", "List of validation errors.", GH_ParamAccess.list);
            pManager.AddTextParameter("Warnings", "W", "List of validation warnings.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "List of informational messages.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string? json = null;
            if (!DA.GetData(0, ref json) || string.IsNullOrEmpty(json))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GhJSON input is required");
                return;
            }

            int levelValue = 1;
            DA.GetData(1, ref levelValue);
            var level = (ValidationLevel)Math.Max(0, Math.Min(2, levelValue));

            bool preferOnline = false;
            DA.GetData(2, ref preferOnline);

            try
            {
                var result = GhJson.Validate(json, level, schemaVersion: null, preferOnline);

                var errors = result.Errors?.Select(e => e.ToString()).ToList() ?? new List<string>();
                var warnings = result.Warnings?.Select(w => w.ToString()).ToList() ?? new List<string>();
                var info = result.Info?.Select(i => i.ToString()).ToList() ?? new List<string>();

                if (!result.IsValid)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"GhJSON validation failed with {errors.Count} error(s)");
                }
                else if (warnings.Count > 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"GhJSON valid with {warnings.Count} warning(s)");
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "GhJSON is valid");
                }

                foreach (var error in errors)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                }

                foreach (var warning in warnings)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
                }

                foreach (var message in info)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, message);
                }

                DA.SetData(0, result.IsValid);
                DA.SetDataList(1, errors);
                DA.SetDataList(2, warnings);
                DA.SetDataList(3, info);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
