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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils.Components
{
    /// <summary>
    /// Provides methods for modifying parameters on non-script components.
    /// Focuses on parameter-level operations: data mapping, reverse, simplify, etc.
    ///
    /// For script component modifications, see <see cref="ScriptModifier"/>.
    /// For component-level operations (lock, preview, bounds), see ComponentManipulation in Utils.Canvas.
    /// </summary>
    public static class ParameterModifier
    {
        #region Parameter Data Settings

        /// <summary>
        /// Sets the data mapping for a parameter (Flatten/Graft/None).
        /// </summary>
        public static void SetDataMapping(IGH_Param param, GH_DataMapping dataMapping)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            param.DataMapping = dataMapping;
            Debug.WriteLine($"[ParameterModifier] Set data mapping to '{dataMapping}' for parameter '{param.Name}'");
        }

        /// <summary>
        /// Sets the Reverse flag for a parameter (reverses list order).
        /// </summary>
        public static void SetReverse(IGH_Param param, bool reverse)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            param.Reverse = reverse;
            Debug.WriteLine($"[ParameterModifier] Set reverse to '{reverse}' for parameter '{param.Name}'");
        }

        /// <summary>
        /// Sets the Simplify flag for a parameter (simplifies geometry).
        /// </summary>
        public static void SetSimplify(IGH_Param param, bool simplify)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            param.Simplify = simplify;
            Debug.WriteLine($"[ParameterModifier] Set simplify to '{simplify}' for parameter '{param.Name}'");
        }

        #endregion

        #region Bulk Parameter Operations

        /// <summary>
        /// Bulk applies data settings to multiple parameters.
        /// </summary>
        public static void BulkApply(
            IEnumerable<IGH_Param> parameters,
            GH_DataMapping? dataMapping = null,
            bool? reverse = null,
            bool? simplify = null)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            int count = 0;
            foreach (var param in parameters)
            {
                if (dataMapping.HasValue)
                    param.DataMapping = dataMapping.Value;
                if (reverse.HasValue)
                    param.Reverse = reverse.Value;
                if (simplify.HasValue)
                    param.Simplify = simplify.Value;
                count++;
            }

            Debug.WriteLine($"[ParameterModifier] Bulk applied settings to {count} parameters");
        }

        /// <summary>
        /// Applies the same modification to multiple parameters.
        /// </summary>
        public static void BatchModify(
            IEnumerable<IGH_Param> parameters,
            Action<IGH_Param> modificationAction)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (modificationAction == null)
                throw new ArgumentNullException(nameof(modificationAction));

            int count = 0;
            foreach (var param in parameters)
            {
                try
                {
                    modificationAction(param);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ParameterModifier] Error modifying parameter '{param.Name}': {ex.Message}");
                }
            }

            Debug.WriteLine($"[ParameterModifier] Batch modified {count} parameters");
        }

        #endregion
    }
}
