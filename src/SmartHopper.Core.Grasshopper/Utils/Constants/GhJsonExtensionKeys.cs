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

namespace SmartHopper.Core.Grasshopper.Utils.Constants
{
    /// <summary>
    /// Constants for GhJSON extension keys used in component state serialization.
    /// </summary>
    public static class GhJsonExtensionKeys
    {
        /// <summary>
        /// Extension key for Python script components.
        /// </summary>
        public const string Python = "gh.python";

        /// <summary>
        /// Extension key for IronPython script components.
        /// </summary>
        public const string IronPython = "gh.ironpython";

        /// <summary>
        /// Extension key for C# script components.
        /// </summary>
        public const string CSharp = "gh.csharp";

        /// <summary>
        /// Extension key for VB Script components.
        /// </summary>
        public const string VBScript = "gh.vbscript";

        /// <summary>
        /// Extension key for SmartHopper ad-hoc parameters.
        /// </summary>
        public const string SmartHopperParameters = "smarthopper.parameters";

        /// <summary>
        /// Property key for script code within extension data.
        /// </summary>
        public const string CodeProperty = "code";

        /// <summary>
        /// Property key for VB script code within extension data.
        /// </summary>
        public const string VBCodeProperty = "vbCode";

        /// <summary>
        /// Property key for VB script within vbCode object.
        /// </summary>
        public const string VBScriptProperty = "script";
    }
}
