/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Models.Serialization
{
    /// <summary>
    /// Utility class for serializing and deserializing Grasshopper documents to/from JSON format.
    /// </summary>
    public static class GrasshopperJsonConverter
    {
        /// <summary>
        /// Default JSON serialization settings with formatting.
        /// </summary>
        private static readonly JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Serialize a Grasshopper document to JSON string.
        /// </summary>
        /// <param name="document">The Grasshopper document to serialize</param>
        /// <param name="settings">Optional JSON serializer settings</param>
        /// <returns>A JSON string representation of the document</returns>
        public static string SerializeToJson(GrasshopperDocument document, JsonSerializerSettings settings = null)
        {
            return JsonConvert.SerializeObject(document, settings ?? DefaultSettings);
        }

        /// <summary>
        /// Deserialize a JSON string to a Grasshopper document.
        /// </summary>
        /// <param name="json">The JSON string to deserialize</param>
        /// <param name="settings">Optional JSON serializer settings</param>
        /// <returns>A Grasshopper document object</returns>
        public static GrasshopperDocument DeserializeFromJson(string json, JsonSerializerSettings settings = null)
        {
            // Parse JSON and fix invalid GUIDs in components and connections
            var jroot = JObject.Parse(json);
            var idMapping = new Dictionary<string, Guid>();
            // Fix component instanceGuids
            if (jroot["components"] is JArray comps)
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
            // Fix connection componentIds
            if (jroot["connections"] is JArray conns)
            {
                foreach (var conn in conns)
                {
                    var fromToken = conn["from"]?["componentId"];
                    if (fromToken != null)
                    {
                        var oldStrFrom = fromToken.ToString();
                        if (idMapping.TryGetValue(oldStrFrom, out var mappedFrom))
                            conn["from"]["componentId"] = mappedFrom.ToString();
                    }
                    var toToken = conn["to"]?["componentId"];
                    if (toToken != null)
                    {
                        var oldStrTo = toToken.ToString();
                        if (idMapping.TryGetValue(oldStrTo, out var mappedTo))
                            conn["to"]["componentId"] = mappedTo.ToString();
                    }
                }
            }
            // Deserialize into document
            return JsonConvert.DeserializeObject<GrasshopperDocument>(jroot.ToString(), settings ?? DefaultSettings);
        }

        /// <summary>
        /// Save a Grasshopper document to a JSON file.
        /// </summary>
        /// <param name="document">The Grasshopper document to save</param>
        /// <param name="filePath">The file path to save to</param>
        /// <param name="settings">Optional JSON serializer settings</param>
        public static void SaveToFile(GrasshopperDocument document, string filePath, JsonSerializerSettings settings = null)
        {
            string json = SerializeToJson(document, settings);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Load a Grasshopper document from a JSON file.
        /// </summary>
        /// <param name="filePath">The file path to load from</param>
        /// <param name="settings">Optional JSON serializer settings</param>
        /// <returns>A Grasshopper document object</returns>
        public static GrasshopperDocument LoadFromFile(string filePath, JsonSerializerSettings settings = null)
        {
            string json = File.ReadAllText(filePath);
            return DeserializeFromJson(json, settings);
        }
    }
}
