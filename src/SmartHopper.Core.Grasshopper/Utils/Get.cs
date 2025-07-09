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

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager.
    /// </summary>
    internal class Get
    {
        /// <summary>
        /// Synonyms for filter tags.
        /// Available filter tokens:
        ///   selected/unselected: component selection on canvas
        ///   enabled/disabled: whether the component can run (enabled = unlocked)
        ///   error/warning/remark: runtime message levels
        ///   previewcapable/notpreviewcapable: supports geometry preview
        ///   previewon/previewoff: current preview toggle.
        /// Synonyms:
        ///   locked → disabled
        ///   unlocked → enabled
        ///   remarks/info → remark
        ///   warn/warnings → warning
        ///   errors → error
        ///   visible → previewon
        ///   hidden → previewoff.
        /// </summary>
        public static readonly Dictionary<string, string> FilterSynonyms = new()
        {
            { "LOCKED", "DISABLED" },
            { "UNLOCKED", "ENABLED" },
            { "REMARKS", "REMARK" },
            { "INFO", "REMARK" },
            { "WARN", "WARNING" },
            { "WARNINGS", "WARNING" },
            { "ERRORS", "ERROR" },
            { "VISIBLE", "PREVIEWON" },
            { "HIDDEN", "PREVIEWOFF" },
        };

        /// <summary>
        /// Synonyms for typeFilter tokens.
        /// Available typeFilter tokens:
        ///   params: only parameter objects (IGH_Param)
        ///   components: only component objects (GH_Component)
        ///   input: components with no incoming connections (inputs only)
        ///   output: components with no outgoing connections (outputs only)
        ///   processing: components with both incoming and outgoing connections
        ///   isolated: components with neither incoming nor outgoing connections (isolated)
        /// Synonyms:
        ///   param, parameter → params
        ///   component, comp → components
        ///   input, inputs → input
        ///   output, outputs → output
        ///   processingcomponents, intermediate, middle, middlecomponents → processing
        ///   isolatedcomponents → isolated.
        /// </summary>
        public static readonly Dictionary<string, string> TypeSynonyms = new()
        {
            { "PARAM", "PARAMS" },
            { "PARAMETER", "PARAMS" },
            { "COMPONENT", "COMPONENTS" },
            { "COMP", "COMPONENTS" },
            { "INPUTS", "INPUT" },
            { "INPUTCOMPONENTS", "INPUT" },
            { "OUTPUTS", "OUTPUT" },
            { "OUTPUTCOMPONENTS", "OUTPUT" },
            { "PROCESSINGCOMPONENTS", "PROCESSING" },
            { "INTERMEDIATE", "PROCESSING" },
            { "MIDDLE", "PROCESSING" },
            { "MIDDLECOMPONENTS", "PROCESSING" },
            { "ISOLATEDCOMPONENTS", "ISOLATED" },
        };

        /// <summary>
        /// Synonyms for categoryFilter tokens.
        /// Available Grasshopper component categories (e.g. Params, Maths, Vector, Curve, Surface, Mesh, etc.).
        /// Maps common abbreviations or alternate names to canonical category tokens.
        /// </summary>
        public static readonly Dictionary<string, string> CategorySynonyms = new()
        {
            { "PARAM", "PARAMS" },
            { "PARAMETERS", "PARAMS" },
            { "MATH", "MATHS" },
            { "VEC", "VECTOR" },
            { "VECTORS", "VECTOR" },
            { "CRV", "CURVE" },
            { "CURVES", "CURVE" },
            { "SURF", "SURFACE" },
            { "SURFS", "SURFACE" },
            { "MESHES", "MESH" },
            { "INT", "INTERSECT" },
            { "TRANS", "TRANSFORM" },
            { "TREE", "SETS" },
            { "TREES", "SETS" },
            { "DATA", "SETS" },
            { "DATASETS", "SETS" },
            { "DIS", "DISPLAY" },
            { "DISP", "DISPLAY" },
            { "VISUALIZATION", "DISPLAY" },
            { "RH", "RHINO" },
            { "RHINOCEROS", "RHINO" },
            { "KANGAROOPHYSICS", "KANGAROO" },
        };

        /// <summary>
        /// Helper to parse include/exclude tokens.
        /// </summary>
        /// <param name="rawGroups">List of raw filter groups.</param>
        /// <param name="synonyms">Dictionary of synonyms for tokens.</param>
        /// <returns>Tuple of include and exclude sets.</returns>
        public static (HashSet<string> Include, HashSet<string> Exclude) ParseIncludeExclude(IEnumerable<string> rawGroups, Dictionary<string, string> synonyms)
        {
            var include = new HashSet<string>();
            var exclude = new HashSet<string>();
            foreach (var rawGroup in rawGroups)
            {
                if (string.IsNullOrWhiteSpace(rawGroup))
                {
                    continue;
                }

                var parts = rawGroup.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var tok = part.Trim();
                    if (string.IsNullOrEmpty(tok))
                    {
                        continue;
                    }

                    bool inc = !tok.StartsWith("-");
                    var tag = tok.TrimStart('+', '-').ToUpperInvariant();
                    if (synonyms != null && synonyms.TryGetValue(tag, out var mapped))
                    {
                        tag = mapped;
                    }

                    if (inc)
                    {
                        include.Add(tag);
                    }
                    else
                    {
                        exclude.Add(tag);
                    }
                }
            }

            return (include, exclude);
        }
    }
}
