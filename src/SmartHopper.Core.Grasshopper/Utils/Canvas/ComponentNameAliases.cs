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
using System.Diagnostics;
using GhJSON.Core.SchemaModels;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Maps informal or abbreviated component names commonly emitted by AI models
    /// to their canonical Grasshopper names so that GhJSON can instantiate them.
    /// This is a lightweight orchestration-layer pre-pass; full fuzzy resolution
    /// lives in GhJSON.Core.NameResolution (available from GhJSON 1.1.0+).
    /// </summary>
    public static class ComponentNameAliases
    {
        /// <summary>
        /// Case-insensitive dictionary mapping informal names to canonical Grasshopper names.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Script components
                { "c#", "C# Script" },
                { "c#script", "C# Script" },
                { "csharp", "C# Script" },
                { "cscript", "C# Script" },
                { "python", "Python 3 Script" },
                { "py", "Python 3 Script" },
                { "pythonscript", "Python 3 Script" },
                { "python3", "Python 3 Script" },
                { "python3script", "Python 3 Script" },
                { "ironpython", "IronPython 2 Script" },

                // Parameter components
                { "slider", "Number Slider" },
                { "numberslider", "Number Slider" },
                { "numslider", "Number Slider" },
                { "panel", "Panel" },
                { "point", "Point" },
                { "pt", "Point" },
                { "curve", "Curve" },
                { "crv", "Curve" },
                { "number", "Number" },
                { "num", "Number" },
                { "integer", "Integer" },
                { "int", "Integer" },
                { "boolean", "Boolean" },
                { "bool", "Boolean" },
                { "toggle", "Boolean Toggle" },
                { "booleantoggle", "Boolean Toggle" },

                // Basic geometry components
                { "line", "Line" },
                { "ln", "Line" },
                { "circle", "Circle" },
                { "circ", "Circle" },
                { "rectangle", "Rectangle" },
                { "rect", "Rectangle" },
                { "box", "Box" },
                { "cube", "Box" },
                { "sphere", "Sphere" },
                { "cylinder", "Cylinder" },
                { "cyl", "Cylinder" },
                { "cone", "Cone" },

                // Plane components
                { "plane", "XY Plane" },
                { "xyplane", "XY Plane" },
                { "xy", "XY Plane" },
                { "xzplane", "XZ Plane" },
                { "xz", "XZ Plane" },
                { "yzplane", "YZ Plane" },
                { "yz", "YZ Plane" },

                // Construct components
                { "constructpoint", "Construct Point" },
                { "ptxyz", "Construct Point" },
                { "xyz", "Construct Point" },
                { "constructplane", "Construct Plane" },
                { "constructvector", "Construct Vector" },
                { "vec", "Vector XYZ" },
                { "vector", "Vector XYZ" },
                { "vectorxyz", "Vector XYZ" },

                // Math components
                { "add", "Addition" },
                { "addition", "Addition" },
                { "plus", "Addition" },
                { "sub", "Subtraction" },
                { "subtraction", "Subtraction" },
                { "minus", "Subtraction" },
                { "mul", "Multiplication" },
                { "multiplication", "Multiplication" },
                { "multiply", "Multiplication" },
                { "div", "Division" },
                { "division", "Division" },
                { "divide", "Division" },
                { "abs", "Absolute" },
                { "absolute", "Absolute" },
                { "neg", "Negative" },
                { "negative", "Negative" },
                { "pow", "Power" },
                { "power", "Power" },
                { "sqrt", "Square Root" },
                { "squareroot", "Square Root" },

                // List components
                { "listitem", "List Item" },
                { "listlength", "List Length" },
                { "reverse", "Reverse List" },
                { "reverselist", "Reverse List" },
                { "sort", "Sort List" },
                { "sortlist", "Sort List" },
                { "flatten", "Flatten" },
                { "graft", "Graft Tree" },
                { "grafttree", "Graft Tree" },

                // Set components
                { "series", "Series" },
                { "range", "Range" },
                { "random", "Random" },
                { "domain", "Construct Domain" },
                { "constructdomain", "Construct Domain" },

                // Transform components
                { "move", "Move" },
                { "rotate", "Rotate" },
                { "scale", "Scale" },
                { "mirror", "Mirror" },
                { "orient", "Orient" },

                // Surface components
                { "loft", "Loft" },
                { "extrude", "Extrude" },
                { "extrudepoint", "Extrude Point" },
                { "sweep", "Sweep1" },
                { "sweep1", "Sweep1" },
                { "sweep2", "Sweep2" },

                // Mesh components
                { "mesh", "Mesh" },
                { "meshbox", "Mesh Box" },
                { "meshsphere", "Mesh Sphere" },

                // Display components
                { "colour", "Colour Swatch" },
                { "color", "Colour Swatch" },
                { "colourswatch", "Colour Swatch" },
                { "colorswatch", "Colour Swatch" },
            };

        /// <summary>
        /// Resolves a single component name to its canonical form.
        /// Returns the canonical name if an alias is found, or the trimmed input otherwise.
        /// </summary>
        /// <param name="name">The component name or alias to resolve.</param>
        /// <returns>The canonical component name, or the trimmed input if no alias matches.</returns>
        public static string Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var trimmed = name.Trim();
            return ((IDictionary<string, string>)Aliases).TryGetValue(trimmed, out var canonical)
                ? canonical
                : trimmed;
        }

        /// <summary>
        /// Normalizes all component names in a GhJSON document by replacing informal
        /// aliases with their canonical Grasshopper names.
        /// </summary>
        /// <param name="document">The GhJSON document to normalize (mutated in place).</param>
        /// <returns>The number of component names that were substituted.</returns>
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
                    Debug.WriteLine($"[ComponentNameAliases] Resolved '{component.Name}' -> '{resolved}'");
                    component.Name = resolved;
                    substitutions++;
                }
            }

            return substitutions;
        }
    }
}
