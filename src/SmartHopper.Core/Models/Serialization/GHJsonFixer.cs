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
        /// Fixes connection instanceIds based on the provided ID mapping.
        /// </summary>
        /// <returns></returns>
        public static (JObject, Dictionary<string, Guid>) FixConnectionComponentIds(JObject json, Dictionary<string, Guid> idMapping)
        {
            if (json["connections"] is JArray conns)
            {
                foreach (var conn in conns)
                {
                    var fromToken = conn["from"]?["instanceId"];
                    if (fromToken != null)
                    {
                        var oldStrFrom = fromToken.ToString();
                        if (idMapping.TryGetValue(oldStrFrom, out var mappedFrom))
                        {
                            conn["from"]["instanceId"] = mappedFrom.ToString();
                        }
                    }

                    var toToken = conn["to"]?["instanceId"];
                    if (toToken != null)
                    {
                        var oldStrTo = toToken.ToString();
                        if (idMapping.TryGetValue(oldStrTo, out var mappedTo))
                        {
                            conn["to"]["instanceId"] = mappedTo.ToString();
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
                    if (comp["pivot"] == null || comp["pivot"]["X"] == null || comp["pivot"]["Y"] == null)
                    {
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
