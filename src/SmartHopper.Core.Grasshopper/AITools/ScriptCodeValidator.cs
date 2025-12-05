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
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Validates generated script code to ensure it uses RhinoCommon/Grasshopper geometry types
    /// instead of generic geometry libraries. Provides correction prompts for self-healing.
    /// </summary>
    public static partial class ScriptCodeValidator
    {
        #region Banned Patterns

        /// <summary>
        /// Patterns that indicate use of non-Rhino geometry libraries.
        /// Each entry contains: regex pattern, human-readable name, and suggested fix.
        /// </summary>
        private static readonly List<(Regex Pattern, string LibraryName, string SuggestedFix)> BannedPatterns = new()
        {
            // .NET generic geometry
            (SystemNumericsVector3Regex(), "System.Numerics.Vector3", "Use Rhino.Geometry.Vector3d or Point3d instead"),
            (SystemNumericsMatrix4x4Regex(), "System.Numerics.Matrix4x4", "Use Rhino.Geometry.Transform instead"),
            (SystemDrawingPointFRegex(), "System.Drawing.PointF/Point", "Use Rhino.Geometry.Point3d instead"),

            // Unity (common AI mistake)
            (UnityEngineVector3Regex(), "UnityEngine.Vector3", "Use Rhino.Geometry.Vector3d or Point3d instead"),
            (UnityEngineQuaternionRegex(), "UnityEngine.Quaternion", "Use Rhino.Geometry.Quaternion instead"),
            (UnityEngineTransformRegex(), "UnityEngine.Transform", "Use Rhino.Geometry.Transform instead"),

            // Python geometry libraries
            (NumpyArrayRegex(), "numpy arrays for geometry", "Use Rhino.Geometry types (Point3d, Vector3d, etc.) instead"),
            (ShapelyImportRegex(), "shapely", "Use Rhino.Geometry.Curve, Brep, etc. instead"),
            (ScipySpatialRegex(), "scipy.spatial", "Use Rhino.Geometry or RhinoCommon methods instead"),
            (TrimeshImportRegex(), "trimesh", "Use Rhino.Geometry.Mesh instead"),
            (Open3dImportRegex(), "open3d", "Use Rhino.Geometry.PointCloud and Mesh instead"),
            (PyvistaMeshRegex(), "pyvista", "Use Rhino.Geometry.Mesh instead"),

            // Generic 3D math (often appears in AI-generated code)
            (GenericVector3ClassRegex(), "custom Vector3 class", "Use Rhino.Geometry.Vector3d instead"),
            (MathNetNumericsRegex(), "MathNet.Numerics vectors", "Use Rhino.Geometry types instead"),
        };

        #endregion

        #region Compiled Regex Patterns

        [GeneratedRegex(@"System\.Numerics\.Vector3", RegexOptions.IgnoreCase)]
        private static partial Regex SystemNumericsVector3Regex();

        [GeneratedRegex(@"System\.Numerics\.Matrix4x4", RegexOptions.IgnoreCase)]
        private static partial Regex SystemNumericsMatrix4x4Regex();

        [GeneratedRegex(@"System\.Drawing\.(PointF?|Point)\b", RegexOptions.IgnoreCase)]
        private static partial Regex SystemDrawingPointFRegex();

        [GeneratedRegex(@"UnityEngine\.Vector3", RegexOptions.IgnoreCase)]
        private static partial Regex UnityEngineVector3Regex();

        [GeneratedRegex(@"UnityEngine\.Quaternion", RegexOptions.IgnoreCase)]
        private static partial Regex UnityEngineQuaternionRegex();

        [GeneratedRegex(@"UnityEngine\.Transform", RegexOptions.IgnoreCase)]
        private static partial Regex UnityEngineTransformRegex();

        [GeneratedRegex(@"np\.(array|zeros|ones)\s*\(\s*\[.*\d.*\]", RegexOptions.IgnoreCase)]
        private static partial Regex NumpyArrayRegex();

        [GeneratedRegex(@"(from\s+shapely|import\s+shapely)", RegexOptions.IgnoreCase)]
        private static partial Regex ShapelyImportRegex();

        [GeneratedRegex(@"(from\s+scipy\.spatial|import\s+scipy\.spatial)", RegexOptions.IgnoreCase)]
        private static partial Regex ScipySpatialRegex();

        [GeneratedRegex(@"(from\s+trimesh|import\s+trimesh)", RegexOptions.IgnoreCase)]
        private static partial Regex TrimeshImportRegex();

        [GeneratedRegex(@"(from\s+open3d|import\s+open3d)", RegexOptions.IgnoreCase)]
        private static partial Regex Open3dImportRegex();

        [GeneratedRegex(@"(from\s+pyvista|import\s+pyvista)", RegexOptions.IgnoreCase)]
        private static partial Regex PyvistaMeshRegex();

        [GeneratedRegex(@"class\s+Vector3\s*[:\(]", RegexOptions.IgnoreCase)]
        private static partial Regex GenericVector3ClassRegex();

        [GeneratedRegex(@"MathNet\.Numerics\.(LinearAlgebra|Vector)", RegexOptions.IgnoreCase)]
        private static partial Regex MathNetNumericsRegex();

        #endregion

        #region Validation Result

        /// <summary>
        /// Result of script code validation.
        /// </summary>
        public class ValidationResult
        {
            /// <summary>
            /// Gets a value indicating whether the script is valid (no banned patterns found).
            /// </summary>
            public bool IsValid { get; init; }

            /// <summary>
            /// Gets the list of issues found in the script.
            /// </summary>
            public List<string> Issues { get; init; } = new();

            /// <summary>
            /// Gets a correction prompt to send back to the AI for self-healing.
            /// </summary>
            public string CorrectionPrompt { get; init; } = string.Empty;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Validates script code for use of non-Rhino/Grasshopper geometry libraries.
        /// </summary>
        /// <param name="scriptCode">The script code to validate.</param>
        /// <param name="language">The scripting language (python, c#, vb, ironpython).</param>
        /// <returns>Validation result with issues and correction prompt if needed.</returns>
        public static ValidationResult Validate(string scriptCode, string language)
        {
            if (string.IsNullOrWhiteSpace(scriptCode))
            {
                return new ValidationResult { IsValid = true };
            }

            var issues = new List<string>();

            foreach (var (pattern, libraryName, suggestedFix) in BannedPatterns)
            {
                if (pattern.IsMatch(scriptCode))
                {
                    issues.Add($"Found '{libraryName}': {suggestedFix}");
                    Debug.WriteLine($"[ScriptCodeValidator] Detected banned pattern: {libraryName}");
                }
            }

            // Language-specific checks
            var languageLower = language?.ToLowerInvariant() ?? "python";

            if (languageLower is "python" or "ironpython")
            {
                // Check for missing Rhino.Geometry import when geometry types are used
                if (UsesGeometryTypes(scriptCode) && !HasRhinoGeometryImport(scriptCode))
                {
                    issues.Add("Script uses geometry types but missing 'import Rhino.Geometry as rg' or equivalent import.");
                }
            }
            else if (languageLower is "c#" or "csharp")
            {
                // Check for missing using statement
                if (UsesGeometryTypes(scriptCode) && !HasCSharpRhinoGeometryUsing(scriptCode))
                {
                    issues.Add("Script uses geometry types but missing 'using Rhino.Geometry;' directive.");
                }
            }

            if (issues.Count == 0)
            {
                return new ValidationResult { IsValid = true };
            }

            // Build correction prompt
            var correctionPrompt = BuildCorrectionPrompt(issues, language);

            return new ValidationResult
            {
                IsValid = false,
                Issues = issues,
                CorrectionPrompt = correctionPrompt,
            };
        }

        /// <summary>
        /// Gets the language-specific guidance to append to system prompts.
        /// </summary>
        /// <param name="language">The scripting language.</param>
        /// <returns>Language-specific guidance string.</returns>
        public static string GetLanguageGuidance(string language)
        {
            var languageLower = language?.ToLowerInvariant() ?? "python";

            return languageLower switch
            {
                "python" or "python3" => GetPythonGuidance(),
                "ironpython" or "ironpython2" => GetIronPythonGuidance(),
                "c#" or "csharp" => GetCSharpGuidance(),
                "vb" or "vb.net" or "vbnet" => GetVBNetGuidance(),
                _ => GetPythonGuidance(),
            };
        }

        #endregion

        #region Private Helpers

        private static bool UsesGeometryTypes(string code)
        {
            // Check for common geometry type names
            return Regex.IsMatch(code, @"\b(Point3d|Vector3d|Curve|Surface|Brep|Mesh|Plane|Line|Circle|Arc|NurbsCurve|NurbsSurface|PolylineCurve|Polyline)\b", RegexOptions.IgnoreCase);
        }

        private static bool HasRhinoGeometryImport(string code)
        {
            return Regex.IsMatch(code, @"(import\s+Rhino\.Geometry|from\s+Rhino\.Geometry\s+import|import\s+Rhino\s*$|from\s+Rhino\s+import)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        private static bool HasCSharpRhinoGeometryUsing(string code)
        {
            return Regex.IsMatch(code, @"using\s+Rhino\.Geometry\s*;", RegexOptions.IgnoreCase);
        }

        private static string BuildCorrectionPrompt(List<string> issues, string language)
        {
            var issuesList = string.Join("\n- ", issues);
            var guidance = GetLanguageGuidance(language);

            return $"""
                The previous script contains non-Rhino/Grasshopper geometry code that will not work in this environment.

                Issues found:
                - {issuesList}

                Please rewrite the script using ONLY RhinoCommon geometry types from the Rhino.Geometry namespace.

                {guidance}

                Rewrite the script to fix these issues while preserving the same behavior.
                """;
        }

        private static string GetPythonGuidance()
        {
            return """
                ## Python 3 (Grasshopper) Geometry Guidelines

                **Required imports:**
                ```python
                import Rhino.Geometry as rg
                from System.Collections.Generic import List
                ```

                **Common type mappings:**
                - Point → `rg.Point3d(x, y, z)`
                - Vector → `rg.Vector3d(x, y, z)`
                - Line → `rg.Line(startPt, endPt)`
                - Plane → `rg.Plane(origin, normal)` or `rg.Plane(origin, xAxis, yAxis)`
                - Circle → `rg.Circle(plane, radius)`
                - Arc → `rg.Arc(plane, radius, angle)`
                - Polyline → `rg.Polyline(points)` then `.ToNurbsCurve()` if needed
                - Box → `rg.Box(plane, interval_x, interval_y, interval_z)`

                **Input/Output:**
                - Inputs are accessed directly by parameter name (e.g., `x`, `points`, `curve`)
                - Outputs are assigned to lowercase letters: `a = result`, `b = other_result`

                **CRITICAL: Outputting lists in Python 3**
                - Output parameters do NOT have 'access' (item/list/tree) settings like inputs.
                - To output multiple items, you MUST use a .NET List[T], NOT a Python list.
                - Python lists cause "type conversion failed from PyObject" errors.

                **Pattern for list outputs:**
                ```python
                from System.Collections.Generic import List

                # Create a .NET List of the appropriate type
                result = List[rg.Curve]()
                for item in items:
                    result.Add(item.ToNurbsCurve())
                a = result
                ```

                **Example (single item output):**
                ```python
                import Rhino.Geometry as rg

                pt = rg.Point3d(x, y, z)
                plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                circle = rg.Circle(plane, radius)
                a = circle.ToNurbsCurve()
                ```

                **Example (list output):**
                ```python
                import Rhino.Geometry as rg
                from System.Collections.Generic import List

                circles = List[rg.Curve]()
                for pt in points:
                    plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                    circle = rg.Circle(plane, radius)
                    circles.Add(circle.ToNurbsCurve())
                a = circles
                ```
                """;
        }

        private static string GetIronPythonGuidance()
        {
            return """
                ## IronPython 2 (Grasshopper) Geometry Guidelines

                **Required import:**
                ```python
                import Rhino.Geometry as rg
                ```

                **Common type mappings:**
                - Point → `rg.Point3d(x, y, z)`
                - Vector → `rg.Vector3d(x, y, z)`
                - Line → `rg.Line(startPt, endPt)`
                - Plane → `rg.Plane(origin, normal)`
                - Circle → `rg.Circle(plane, radius)`

                **Input/Output:**
                - Inputs are accessed directly by parameter name
                - Outputs are assigned to lowercase letters: `a = result`

                **Note:** IronPython 2 uses Python 2 syntax (print as statement, no f-strings).

                **Outputting lists in IronPython 2:**
                - Output parameters do NOT have 'access' settings like inputs.
                - IronPython 2 can use Python lists directly for output (unlike Python 3).
                - Simply assign the list to the output variable.

                **Example (single item):**
                ```python
                import Rhino.Geometry as rg

                pt = rg.Point3d(x, y, z)
                plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                circle = rg.Circle(plane, radius)
                a = circle.ToNurbsCurve()
                ```

                **Example (list output):**
                ```python
                import Rhino.Geometry as rg

                circles = []
                for pt in points:
                    plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                    circle = rg.Circle(plane, radius)
                    circles.append(circle.ToNurbsCurve())
                a = circles
                ```
                """;
        }

        private static string GetCSharpGuidance()
        {
            return """
                ## C# (Grasshopper) Geometry Guidelines

                **Required using directives:**
                ```csharp
                using Rhino.Geometry;
                using System.Collections.Generic;
                ```

                **Common type mappings:**
                - Point → `new Point3d(x, y, z)`
                - Vector → `new Vector3d(x, y, z)`
                - Line → `new Line(startPt, endPt)`
                - Plane → `new Plane(origin, normal)` or `Plane.WorldXY`
                - Circle → `new Circle(plane, radius)`
                - Arc → `new Arc(plane, radius, angle)`
                - Polyline → `new Polyline(points)`
                - Box → `new Box(plane, interval_x, interval_y, interval_z)`
                - Transform → `Transform.Translation(vector)`, `Transform.Rotation(angle, axis, center)`

                **Outputting lists in C#:**
                - Output parameters do NOT have 'access' settings like inputs.
                - To output multiple items, assign a `List<T>` to the output parameter.
                - Grasshopper handles the conversion automatically.

                **Example (single item output):**
                ```csharp
                var circle = new Circle(new Plane(pt, Vector3d.ZAxis), radius);
                A = circle.ToNurbsCurve();
                ```

                **Example (list output):**
                ```csharp
                var circles = new List<Curve>();
                foreach (var pt in points)
                {
                    var circle = new Circle(new Plane(pt, Vector3d.ZAxis), radius);
                    circles.Add(circle.ToNurbsCurve());
                }
                A = circles;
                ```

                **Important:**
                - Use `Rhino.Geometry` types, NOT `System.Numerics` or `UnityEngine`
                - Use `Point3d` not `Vector3` for positions
                - Use `Vector3d` not `Vector3` for directions
                - Use `Transform` not `Matrix4x4` for transformations
                """;
        }

        private static string GetVBNetGuidance()
        {
            return """
                ## VB.NET (Grasshopper) Geometry Guidelines

                **Required imports:**
                ```vb
                Imports Rhino.Geometry
                Imports System.Collections.Generic
                ```

                **Common type mappings:**
                - Point → `New Point3d(x, y, z)`
                - Vector → `New Vector3d(x, y, z)`
                - Line → `New Line(startPt, endPt)`
                - Plane → `New Plane(origin, normal)`
                - Circle → `New Circle(plane, radius)`

                **Outputting lists in VB.NET:**
                - Output parameters do NOT have 'access' settings like inputs.
                - To output multiple items, assign a `List(Of T)` to the output parameter.

                **Example (single item output):**
                ```vb
                Dim circle As New Circle(New Plane(pt, Vector3d.ZAxis), radius)
                A = circle.ToNurbsCurve()
                ```

                **Example (list output):**
                ```vb
                Dim circles As New List(Of Curve)
                For Each pt In points
                    Dim circle As New Circle(New Plane(pt, Vector3d.ZAxis), radius)
                    circles.Add(circle.ToNurbsCurve())
                Next
                A = circles
                ```
                """;
        }

        #endregion
    }
}
