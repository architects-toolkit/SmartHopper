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
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Models.Serialization
{
    /// <summary>
    /// Provides methods to fix JSON structure issues in GhJSON before deserialization.
    /// </summary>
    public static class GHJsonFixer
    {
        /// <summary>
        /// Fixes invalid component instanceGuids by assigning new GUIDs and recording mappings.
        /// </summary>
        /// <returns></returns>
        public static (JObject, Dictionary<string, Guid>) FixComponentInstanceGuids(JObject json, Dictionary<string, Guid> idMapping)
        {
            if (json["components"] is JArray comps)
            {
                foreach (var comp in comps)
                {
                    if (comp["instanceGuid"] is JToken instToken)
                    {
                        var instStr = instToken.ToString();
                        if (!Guid.TryParse(instStr, out _))
                        {
                            var newGuid = Guid.NewGuid();
                            idMapping[instStr] = newGuid;
                            comp["instanceGuid"] = newGuid.ToString();
                        }
                    }
                }
            }

            return (json, idMapping);
        }

        /// <summary>
        /// Removes pivot properties if not all components define a pivot.
        /// </summary>
        /// <returns></returns>
        public static JObject RemovePivotsIfIncomplete(JObject json)
        {
            if (json["components"] is JArray comps)
            {
                bool allHavePivot = true;
                foreach (var comp in comps)
                {
                    var pivotToken = comp["pivot"];
                    if (pivotToken == null)
                    {
                        allHavePivot = false;
                        break;
                    }

                    // Handle both old object format {"X": ..., "Y": ...} and new compact string format "X,Y"
                    if (pivotToken.Type == JTokenType.Object)
                    {
                        // Old format - check for X and Y properties
                        if (pivotToken["X"] == null || pivotToken["Y"] == null)
                        {
                            allHavePivot = false;
                            break;
                        }
                    }
                    else if (pivotToken.Type == JTokenType.String)
                    {
                        // New compact format - check if string is not empty
                        var pivotStr = pivotToken.ToString();
                        if (string.IsNullOrEmpty(pivotStr) || !pivotStr.Contains(","))
                        {
                            allHavePivot = false;
                            break;
                        }
                    }
                    else
                    {
                        // Unknown format
                        allHavePivot = false;
                        break;
                    }
                }

                if (!allHavePivot)
                {
                    foreach (var comp in comps)
                    {
                        ((JObject)comp).Remove("pivot");
                    }
                }
            }

            return json;
        }
    }
}
