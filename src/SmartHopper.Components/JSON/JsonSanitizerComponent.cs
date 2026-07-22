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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that attempts to recover a valid JSON object from a
    /// malformed / AI-generated string. Runs the full <see cref="JsonFormatHelper"/>
    /// recovery pipeline (direct parse → strip markdown fences → brace-depth extract →
    /// sanitize unescaped control chars, smart quotes, trailing commas, and close
    /// unbalanced containers) and emits both the minified JSON and a human-readable
    /// summary of the steps performed.
    /// </summary>
    public class JsonSanitizerComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("83A970D9-B812-46C7-8475-717076032B4D");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonobj;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSanitizerComponent"/> class.
        /// </summary>
        public JsonSanitizerComponent()
            : base(
                "JSON Sanitizer",
                "JsonSanitize",
                "Attempts to recover a valid JSON object from a malformed or AI-generated string.\n" +
                "Pipeline: direct parse → strip markdown code-block fences → extract first complete JSON object via brace-depth tracking → sanitize (escape control chars inside strings, normalize smart quotes, remove trailing commas, close unterminated strings and unbalanced braces/brackets).\n" +
                "Outputs the minified JSON and a summary of the operations performed.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "Potentially malformed JSON string (may be wrapped in markdown, contain prose, have unescaped newlines, etc.)", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Minify", "M", "If true, output is collapsed to a single line (no structural whitespace, no line breaks). If false or omitted, output is human-readable indented JSON. Default: false.", GH_ParamAccess.item, false);
            pManager[1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "Sanitized JSON string — single-line minified when Minify is true, otherwise indented/human-readable. Empty if recovery failed.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "S", "Human-readable summary of the recovery steps performed, one line per step.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Is Object", "O", "True when the recovered root is a JSON object (`{...}`); false otherwise, including on failure.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Is Array", "A", "True when the recovered root is a JSON array (`[...]`); false otherwise, including on failure.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Tree-in / tree-out with preserved path structure.
            if (!DA.GetDataTree<GH_String>("JSON", out GH_Structure<GH_String> inputTree))
            {
                return;
            }

            bool minify = false;
            DA.GetData("Minify", ref minify);

            var jsonOut = new GH_Structure<GH_String>();
            var summaryOut = new GH_Structure<GH_String>();
            var isObjectOut = new GH_Structure<GH_Boolean>();
            var isArrayOut = new GH_Structure<GH_Boolean>();
            bool anyFailure = false;

            for (int b = 0; b < inputTree.PathCount; b++)
            {
                var path = inputTree.Paths[b];
                var branch = inputTree.Branches[b];

                for (int i = 0; i < branch.Count; i++)
                {
                    string raw = branch[i]?.Value;
                    SanitizeItem(raw, minify, out string serialized, out string summary, out bool recovered, out bool isObject, out bool isArray);

                    jsonOut.Append(new GH_String(serialized), path);
                    summaryOut.Append(new GH_String(summary), path);
                    isObjectOut.Append(new GH_Boolean(isObject), path);
                    isArrayOut.Append(new GH_Boolean(isArray), path);

                    if (!recovered && !string.IsNullOrWhiteSpace(raw))
                    {
                        anyFailure = true;
                    }
                }
            }

            DA.SetDataTree(0, jsonOut);
            DA.SetDataTree(1, summaryOut);
            DA.SetDataTree(2, isObjectOut);
            DA.SetDataTree(3, isArrayOut);

            if (anyFailure)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "One or more items could not be recovered as valid JSON. See Summary output for details.");
            }
        }

        /// <summary>
        /// Runs the recovery pipeline on a single item and serializes the recovered token
        /// according to <paramref name="minify"/>:
        /// <list type="bullet">
        ///   <item><description><c>true</c> → <see cref="Formatting.None"/> plus a defensive strip of any stray CR/LF → guaranteed single-line output.</description></item>
        ///   <item><description><c>false</c> → <see cref="Formatting.Indented"/> → human-readable pretty-printed JSON (with line breaks and indentation).</description></item>
        /// </list>
        /// </summary>
        private static void SanitizeItem(string raw, bool minify, out string serialized, out string summary, out bool recovered, out bool isObject, out bool isArray)
        {
            isObject = false;
            isArray = false;

            if (string.IsNullOrWhiteSpace(raw))
            {
                serialized = string.Empty;
                summary = "Input is empty or whitespace.";
                recovered = false;
                return;
            }

            // Use the token-level recovery so JSON arrays at the root are preserved as arrays
            // rather than being narrowed to just the first object element.
            recovered = JsonFormatHelper.TryRecoverJsonToken(raw, out var token, out var log);
            summary = string.Join(Environment.NewLine, log);

            if (recovered && token != null)
            {
                isObject = token is JObject;
                isArray = token is JArray;

                if (minify)
                {
                    // Formatting.None → no structural whitespace. Belt-and-braces strip of any
                    // stray raw newlines that might have slipped through to guarantee a true
                    // single-line output regardless.
                    serialized = token.ToString(Formatting.None).Replace("\r", string.Empty).Replace("\n", string.Empty);
                }
                else
                {
                    // Human-readable pretty-print with line breaks and indentation.
                    serialized = token.ToString(Formatting.Indented);
                }
            }
            else
            {
                serialized = string.Empty;
            }
        }
    }
}
