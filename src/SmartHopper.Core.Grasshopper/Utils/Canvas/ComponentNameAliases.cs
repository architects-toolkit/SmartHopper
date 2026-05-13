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

/*
 * Alias list and overall approach derived from Cordyceps
 * (https://github.com/brookstalley/cordyceps), Core/ComponentRegistry.cs.
 * Copyright (c) 2026 Brooks Talley. Licensed under the MIT License.
 *
 * The Cordyceps alias map is reused as a starting point for the LLM-style
 * loose component-name resolution that sits between AI-emitted GhJSON and
 * the strict resolver in GhJSON.Grasshopper. Additions and modifications
 * for SmartHopper are licensed under LGPL-3.0 (see header above).
 */

using System;
using System.Collections.Generic;
using GhJSON.Core.SchemaModels;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Loose, LLM-style component name resolution that maps common informal
    /// names (e.g. "csharp", "slider", "python") to canonical Grasshopper
    /// component names ("C# Script", "Number Slider", "Python 3 Script") before
    /// a GhJSON document is handed off to <c>GhJSON.Grasshopper</c> for placement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GhJSON.Grasshopper.Deserialization.ComponentInstantiator</c> resolves
    /// component names with <see cref="StringComparison.OrdinalIgnoreCase"/> against
    /// the proxy registry. LLMs frequently emit informal aliases that do not match
    /// any registered proxy. Pre-processing the GhJSON document with this helper
    /// reduces self-healing retry loops and improves first-shot tool-call success
    /// rates without weakening the strict schema validation in GhJSON.Core.
    /// </para>
    /// <para>
    /// All resolution is local string substitution; no Rhino or Grasshopper APIs
    /// are touched, so this helper is safe to unit-test without Rhino runtime
    /// activation.
    /// </para>
    /// </remarks>
    public static class ComponentNameAliases
    {
        /// <summary>
        /// Map of informal component names to their canonical Grasshopper names.
        /// </summary>
        /// <remarks>
        /// Keys are compared case-insensitively. Values must be names that
        /// <c>GhJSON.Grasshopper</c> can resolve via the active component server.
        /// </remarks>
        public static readonly IReadOnlyDictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Script components
                { "c#", "C# Script" },
                { "csharp", "C# Script" },
                { "csharp script", "C# Script" },
                { "c# component", "C# Script" },
                { "python", "Python 3 Script" },
                { "python script", "Python 3 Script" },
                { "python3", "Python 3 Script" },
                { "py", "Python 3 Script" },
                { "ghpython", "Python 3 Script" },

                // Planes
                { "plane", "XY Plane" },
                { "xyplane", "XY Plane" },
                { "xy", "XY Plane" },
                { "xzplane", "XZ Plane" },
                { "xz", "XZ Plane" },
                { "yzplane", "YZ Plane" },
                { "yz", "YZ Plane" },

                // Geometry shortcuts
                { "cube", "Box" },
                { "rect", "Rectangle" },
                { "circ", "Circle" },
                { "cyl", "Cylinder" },
                { "pt", "Point" },
                { "ln", "Line" },
                { "crv", "Curve" },

                // Parameters and primitives
                { "slider", "Number Slider" },
                { "numberslider", "Number Slider" },
                { "number slider", "Number Slider" },
                { "num", "Number" },
                { "int", "Integer" },
                { "bool", "Boolean" },
                { "str", "Text" },
                { "string", "Text" },

                // Stream filter
                { "streamfilter", "Stream Filter" },
                { "filter", "Stream Filter" },
            };

        /// <summary>
        /// Resolves a single informal component name to its canonical form, or
        /// returns the input unchanged when no alias is registered.
        /// </summary>
        /// <param name="name">Component name as emitted by the AI / user.</param>
        /// <returns>Canonical component name or <paramref name="name"/> verbatim.</returns>
        public static string Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var trimmed = name.Trim();
            return Aliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
        }

        /// <summary>
        /// Walks a GhJSON document and replaces every recognised informal
        /// component name with its canonical form, in place.
        /// </summary>
        /// <param name="document">GhJSON document to normalize. May be null or empty.</param>
        /// <returns>Number of component names that were substituted.</returns>
        public static int Normalize(GhJsonDocument? document)
        {
            if (document?.Components == null)
            {
                return 0;
            }

            var substitutions = 0;
            foreach (var component in document.Components)
            {
                if (component == null || string.IsNullOrWhiteSpace(component.Name))
                {
                    continue;
                }

                var resolved = Resolve(component.Name);
                if (!string.Equals(resolved, component.Name, StringComparison.Ordinal))
                {
                    component.Name = resolved;
                    substitutions++;
                }
            }

            return substitutions;
        }
    }
}
