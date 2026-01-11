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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

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

        // C# script structure patterns (detect method declarations that shouldn't be there)
        [GeneratedRegex(@"^\s*(private|public|protected|internal)\s+(void|static|async|object|string|int|double|bool|var)\s+\w+\s*\(", RegexOptions.Multiline)]
        private static partial Regex CSharpMethodDeclarationRegex();

        [GeneratedRegex(@"^\s*void\s+RunScript\s*\(", RegexOptions.Multiline)]
        private static partial Regex CSharpRunScriptMethodRegex();

        [GeneratedRegex(@"^\s*(namespace|class|struct)\s+\w+", RegexOptions.Multiline)]
        private static partial Regex CSharpNamespaceClassRegex();

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

                // Check for method declarations (RunScript, private void, etc.)
                if (CSharpRunScriptMethodRegex().IsMatch(scriptCode))
                {
                    issues.Add("C# script must NOT include 'void RunScript(...)' method declaration. Grasshopper wraps your code automatically. Write only the method BODY - the statements that go inside RunScript.");
                }
                else if (CSharpMethodDeclarationRegex().IsMatch(scriptCode))
                {
                    issues.Add("C# script must NOT include access modifiers (private, public, etc.) or method declarations at the top level. Write only the code that goes inside the RunScript method body.");
                }

                // Check for namespace/class declarations
                if (CSharpNamespaceClassRegex().IsMatch(scriptCode))
                {
                    issues.Add("C# script must NOT include namespace, class, or struct declarations. Write only the code that goes inside the RunScript method body.");
                }

                // Check for shebang / language specifier directives (Python-only feature)
                if (Regex.IsMatch(scriptCode, @"^\s*#!", RegexOptions.Multiline))
                {
                    issues.Add("Other script languages than Python must NOT start with '#!' shebang or language header lines (for example '#! c#').");
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
                The previous script contains issues that will not work correctly in this Grasshopper scripting environment.

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
                ## Python 3 (Grasshopper Script Component) Guidelines

                **Language Specifier Directive (Python-only):**
                For **Python 3 scripts only**, you may start the script with `#! python 3` to specify CPython 3 as the language.
                This directive is embedded in the script as a comment pattern to determine the language.
                It is **only valid for Python scripts**. Do **not** use any `#!` language header (for example `#! c#`, `#! vb`, or `#! csharp`) in C#, VB.NET, or other non-Python scripts.
                ```python
                #! python 3
                ```

                **Required imports:**
                ```python
                import Rhino.Geometry as rg
                from System.Collections.Generic import List
                ```

                **Type Safety:**
                Python is NOT type-safe. Variables can hold any type, but operations will fail at runtime if types are incompatible. Always ensure inputs have values before performing operations.

                **Assembly References:**
                You can reference .NET assemblies using the `#r` directive:
                ```python
                #r "System.Text.Json.dll"
                from System.Text.Json import JsonElement
                ```

                **NuGet Packages:**
                ```python
                #r "nuget: RestSharp, 110.2.0"
                import RestSharp as RS
                ```

                **PyPI Packages:**
                ```python
                # requirements: numpy
                import numpy as np
                ```

                **Module Search Paths:**
                ```python
                # env: C:\Path\To\MyPythonModules\
                import my_module
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

                **Input/Output (Script-Mode):**
                - Inputs are accessed directly by parameter name (e.g., `x`, `points`, `curve`)
                - Outputs are assigned to lowercase letters: `a = result`, `b = other_result`

                **CRITICAL: Outputting lists in Python 3**
                - Output parameters do NOT have 'access' (item/list/tree) settings like inputs.
                - To output multiple items, you MUST use a .NET List[T], NOT a Python list.
                - Python lists cause "type conversion failed from PyObject" errors.

                **Marshalling Data Types (CPython):**
                - When working with Python 3 (CPython), dotnet types are automatically marshalled to Python types.
                - Input `List<int>` becomes a python list. This can be toggled via "Avoid Marshalling Inputs".
                - Output Python lists are converted to dotnet `List<object>`. This can be toggled via "Avoid Marshalling Outputs".
                - For multiple Python 3 components working together, you can avoid marshalling for performance.

                **Pattern for list outputs:**
                ```python
                #! python 3
                from System.Collections.Generic import List

                # Create a .NET List of the appropriate type
                result = List[rg.Curve]()
                for item in items:
                    result.Add(item.ToNurbsCurve())
                a = result
                ```

                **Example (single item output):**
                ```python
                #! python 3
                import Rhino.Geometry as rg

                pt = rg.Point3d(x, y, z)
                plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                circle = rg.Circle(plane, radius)
                a = circle.ToNurbsCurve()
                ```

                **Example (list output):**
                ```python
                #! python 3
                import Rhino.Geometry as rg
                from System.Collections.Generic import List

                circles = List[rg.Curve]()
                for pt in points:
                    plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                    circle = rg.Circle(plane, radius)
                    circles.Add(circle.ToNurbsCurve())
                a = circles
                ```

                **SDK-Mode (Advanced):**
                For advanced functionality (BeforeSolveInstance, AfterSolveInstance, custom preview drawing),
                you can use SDK-Mode by implementing a class that inherits from `GH_ScriptInstance`:
                ```python
                #! python 3
                class MyComponent(Grasshopper.Kernel.GH_ScriptInstance):
                    def RunScript(self, x: int, y: Rhino.Geometry.Plane):
                        a = self.compute_a(x)
                        b = self.compute_b(y)
                        return a, b
                ```
                The RunScript signature automatically includes component inputs with type hints for autocompletion.
                Return values correspond to output parameters in order.
                """;
        }

        private static string GetIronPythonGuidance()
        {
            return """
                ## IronPython 2 (Grasshopper Script Component) Guidelines

                **Language Specifier Directive (Python-only):**
                For **IronPython 2 scripts only**, you may start the script with `#! python 2` to specify IronPython 2 as the language.
                This directive is **only valid for Python-family scripts**. Do **not** use any `#!` language header for C#, VB.NET, or other non-Python scripts (for example `#! c#`).
                ```python
                #! python 2
                ```

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
                - Arc → `rg.Arc(plane, radius, angle)`
                - Polyline → `rg.Polyline(points)`

                **Input/Output:**
                - Inputs are accessed directly by parameter name
                - Outputs are assigned to lowercase letters: `a = result`

                **Assembly References:**
                You can reference .NET assemblies using the `#r` directive:
                ```python
                #r "System.Text.Json.dll"
                from System.Text.Json import JsonElement
                ```

                **Python 2 Syntax Notes:**
                - Use `print "text"` not `print("text")` for print statements
                - No f-strings; use `"text %s" % variable` or `"text {}".format(variable)`
                - Use `xrange()` instead of `range()` for large iterations
                - Integer division: `5/2 = 2` (use `5.0/2` for `2.5`)

                **Outputting lists in IronPython 2:**
                - Output parameters do NOT have 'access' settings like inputs.
                - IronPython 2 can use Python lists directly for output (unlike Python 3).
                - Simply assign the list to the output variable.
                - IronPython seamlessly integrates with .NET types.

                **Example (single item):**
                ```python
                #! python 2
                import Rhino.Geometry as rg

                pt = rg.Point3d(x, y, z)
                plane = rg.Plane(pt, rg.Vector3d.ZAxis)
                circle = rg.Circle(plane, radius)
                a = circle.ToNurbsCurve()
                ```

                **Example (list output):**
                ```python
                #! python 2
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
                ## C# (Grasshopper Script Component) Guidelines

                **CRITICAL: Script Structure**
                Grasshopper C# script components automatically wrap your code inside a class with a `RunScript` method.
                You must write the code that would normally go inside that method.

                **You MAY include at the top of the script:**
                - Standard `using` directives such as `using System;`, `using System.Collections.Generic;`, and `using Rhino.Geometry;`.

                **DO NOT INCLUDE:**
                - `namespace` declarations
                - `class` or `struct` declarations
                - Method declarations like `void RunScript(...)` or `private void MyMethod(...)`
                - Access modifiers (`private`, `public`, `protected`, `internal`) at the top level
                - Any `#!` or shebang-style language header lines (for example `#! c#`, `#! cs`, or `#! csharp`). Shebang directives are only valid for Python scripts and must never appear in C# scripts.

                **CORRECT SCRIPT FORMAT:**
                The script should start with optional `using` statements, followed by variable declarations and statements:
                ```csharp
                using System;
                using System.Collections.Generic;
                using Rhino.Geometry;

                List<Circle> circles = new List<Circle>();

                if (grid == null || grid.Count == 0 || center == null)
                {
                    A = circles;
                    return;
                }

                double scaleFactor = scale > 0 ? scale : 1.0;

                foreach (Point3d pt in grid)
                {
                    double distance = pt.DistanceTo(center);
                    double radius = distance * scaleFactor;

                    Plane plane = new Plane(pt, Vector3d.ZAxis);
                    Circle circle = new Circle(plane, radius);
                    circles.Add(circle);
                }

                A = circles;
                ```

                **INCORRECT SCRIPT FORMAT (DO NOT DO THIS):**
                ```csharp

                // WRONG - do not include method declaration
                private void RunScript(List<Point3d> grid, double radius, ref object A)
                {
                    // ...
                }

                ```

                **Variables and Data Types:**
                - Declare variables with explicit types: `int x = 10;`, `double pi = 3.1415;`, `bool pass = true;`
                - Use `var` when the type is obvious: `var points = new List<Point3d>();`
                - C# is type-safe: a variable's type cannot change after declaration.

                **Collections:**
                - **Arrays** (fixed size): `string[] weekdays = {"Mon", "Tue", "Wed"};`
                - **Lists** (dynamic): `List<Point3d> pts = new List<Point3d>(); pts.Add(new Point3d(0,0,0));`
                - Array indices are zero-based: first element is `array[0]`, last is `array[array.Length - 1]`.
                - List indices are also zero-based: first is `list[0]`, last is `list[list.Count - 1]`.

                **Loops:**
                - **for loop**: `for (int i = 0; i < 10; i++) { ... }`
                - **foreach loop**: `foreach (Point3d pt in points) { ... }`
                - **while loop**: `while (condition) { ... }`
                - Use `break` to exit a loop, `continue` to skip to next iteration.

                **Input/Output:**
                - Input parameters are available by their names (e.g., `grid`, `radius`, `center`).
                - Inputs with List access: `List<Point3d>`, with Tree access: `DataTree<Point3d>`.
                - Output parameters are `A`, `B`, `C`, etc. - just assign values to them.
                - Output parameters are passed by reference (`ref object A`).

                **Rhino.Geometry Types:**
                - **Point3d**: `new Point3d(x, y, z)`, `pt.X`, `pt.Y`, `pt.Z`, `pt.DistanceTo(other)`
                - **Vector3d**: `new Vector3d(x, y, z)`, `v.Unitize()`, `v.Length`, `Vector3d.CrossProduct(a, b)`
                - **Line**: `new Line(startPt, endPt)`, `line.PointAt(t)`, `line.Length`
                - **Plane**: `new Plane(origin, normal)`, `Plane.WorldXY`, `plane.ClosestPoint(pt)`
                - **Circle**: `new Circle(plane, radius)`, `circle.ToNurbsCurve()`
                - **Arc**: `new Arc(plane, radius, angle)`, `arc.ToNurbsCurve()`
                - **Polyline**: `new Polyline(points)`, `pline.ToNurbsCurve()`
                - **Box**: `new Box(plane, xInterval, yInterval, zInterval)`
                - **Interval**: `new Interval(min, max)`
                - **Transform**: `Transform.Translation(vec)`, `Transform.Rotation(angle, axis, center)`

                **Curves:**
                - **NurbsCurve**: `Curve.CreateControlPointCurve(points, degree)`
                - Domain: `crv.Domain`, Points: `crv.PointAtStart`, `crv.PointAtEnd`
                - Tangent: `crv.TangentAtStart`, Divide: `crv.DivideByCount(num, true, out points)`

                **Surfaces:**
                - **NurbsSurface**: `NurbsSurface.CreateFromPoints(points, uCount, vCount, uDegree, vDegree)`
                - **PlaneSurface**: `new PlaneSurface(plane, xInterval, yInterval)`
                - **Extrusion**: `Extrusion.Create(curve, height, cap)`
                - Evaluate: `srf.PointAt(u, v)`, Check: `srf.IsClosed(0)`, `srf.IsPlanar()`

                **Meshes:**
                - Create: `new Mesh()`, then `mesh.Vertices.Add(pt)`, `mesh.Faces.AddFace(i0, i1, i2, i3)`
                - Finalize: `mesh.Normals.ComputeNormals()`, `mesh.Compact()`
                - From surface: `Mesh.CreateFromBrep(brep, MeshingParameters.Default)`

                **Breps (Boundary Representation):**
                - Create: `Brep.CreateFromBox(corners)`, `Brep.CreateFromLoft(curves, ...)`
                - Boolean: `Brep.CreateBooleanUnion(breps, tolerance)`
                - Properties: `brep.IsSolid`, `brep.GetArea()`, `brep.GetVolume()`

                **Transformations:**
                - Translation: `Transform.Translation(vector)`
                - Rotation: `Transform.Rotation(angle, axis, center)`
                - Scale: `Transform.Scale(plane, scaleX, scaleY, scaleZ)`
                - Apply: `geometry.Transform(xform)` or `geometry.Scale(factor)`

                **Outputting lists:**
                - Output parameters do NOT have 'access' (item/list/tree) settings like inputs.
                - To output multiple items, assign a `List<T>` to the output parameter.
                - Grasshopper handles the conversion automatically.

                **Important:**
                - Use `Rhino.Geometry` types, NOT `System.Numerics` or `UnityEngine`.
                - Use `Point3d` not `Vector3` for positions.
                - Use `Vector3d` not `Vector3` for directions.
                - Use `Transform` not `Matrix4x4` for transformations.
                - Comments: `// single line` or `/* multi-line */`
                - Namespaces: `System.Math.Sqrt(num)` or use `using System;` then `Math.Sqrt(num)`
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
