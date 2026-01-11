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
using System.Text.RegularExpressions;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    public static partial class DataTreeConverter
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for removing list indices in parentheses from path keys.
        /// </summary>
        [GeneratedRegex(@"\(\d+\)")]
        private static partial Regex ListIndicesRegex();

        #endregion

        public static Dictionary<string, List<object>> IGHStructureToDictionary(IGH_Structure structure)
        {
            Dictionary<string, List<object>> result = new();

            foreach (GH_Path path in structure.Paths)
            {
                List<object> dataList = new();

                // Iterate over the data items in the path
                foreach (object dataItem in structure.get_Branch(path))
                {
                    // Serialize complex types using DataTypeSerializer
                    object serializedItem = SerializeDataItem(dataItem);
                    dataList.Add(serializedItem);

                    // Debug.WriteLine($"{path.ToString()}: {dataItem.ToString()}");
                }

                // Add the path and list to the dictionary
                result.Add(path.ToString(), dataList);
            }

            return result;
        }

        /// <summary>
        /// Serializes a data item, converting complex types to inline prefixed format.
        /// </summary>
        /// <param name="dataItem">The data item to serialize.</param>
        /// <returns>A serialized representation (string with inline type prefix or simple value).</returns>
        private static object SerializeDataItem(object dataItem)
        {
            if (dataItem == null)
            {
                return null;
            }

            // Unwrap IGH_Goo types to get the actual value
            object actualValue = dataItem;
            if (dataItem is IGH_Goo goo)
            {
                actualValue = goo.ScriptVariable();
            }

            if (actualValue == null)
            {
                return null;
            }

            // Check if we have a serializer for this type
            Type valueType = actualValue.GetType();
            if (DataTypeSerializer.IsTypeSupported(valueType))
            {
                try
                {
                    // Return inline prefixed format: "typePrefix:serializedValue"
                    string serializedValue = DataTypeSerializer.Serialize(actualValue);
                    return serializedValue;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DataTreeConverter] Error serializing {valueType.Name}: {ex.Message}");
                    return actualValue;
                }
            }

            // Return as-is for simple types (string, number, bool)
            return actualValue;
        }

        public static Dictionary<string, object> IGHStructureDictionaryTo1DDictionary(Dictionary<string, List<object>> dictionary)
        {
            Dictionary<string, object> result = new();

            foreach (var kvp in dictionary)
            {
                if (kvp.Value is List<object> list)
                {
                    var tempDict = new Dictionary<string, object>();
                    int index = 0;
                    foreach (var val in list)
                    {
                        tempDict.Add($"{kvp.Key}({index++})", val);
                    }

                    result.Add(kvp.Key, tempDict);
                }
                else
                {
                    result.Add(kvp.Key, new { kvp.Key, kvp.Value });
                }
            }

            return result;
        }

        public static GH_Structure<T> JObjectToIGHStructure<T>(JToken input, Func<JToken, T> convertFunction)
            where T : IGH_Goo
        {
            GH_Structure<T> result = new();

            // Handle JArray input
            if (input is JArray array)
            {
                var defaultPath = new GH_Path(0);
                foreach (var value in array)
                {
                    result.Append(convertFunction(value), defaultPath);
                }

                return result;
            }

            // Handle JObject input
            if (input is JObject jObject)
            {
                foreach (var path in jObject)
                {
                    GH_Path p = new(ParseKeyToPath(path.Key));
                    JObject items = (JObject)path.Value;

                    foreach (var item in items)
                    {
                        foreach (var property in item.Value as JObject)
                        {
                            if (property.Key == "Value" || property.Key == "value")
                            {
                                // Check if this is a complex type with inline prefix
                                JToken valueToken = property.Value;
                                string valueString = valueToken.ToString();

                                // Try to detect and deserialize inline prefixed types automatically
                                if (DataTypeSerializer.TryDeserializeFromPrefix(valueString, out object deserializedValue))
                                {
                                    valueToken = JToken.FromObject(deserializedValue);
                                    Debug.WriteLine($"{p} deserialized from inline format: {valueString}");
                                }

                                result.Append(convertFunction(valueToken), p);
                                Debug.WriteLine($"{p} value found to be: {valueToken}");
                            }
                        }
                    }
                }

                return result;
            }

            // Handle single value input
            result.Append(convertFunction(input), new GH_Path(0));
            return result;
        }

        private static GH_Path ParseKeyToPath(string key)
        {
            // Remove list indices in parentheses and split the key by semicolons
            string cleanedKey = ListIndicesRegex().Replace(key, string.Empty);
            var pathElements = cleanedKey.Trim('{', '}').Split(';');

            // Convert the path elements to integers and create a new GH_Path
            List<int> indices = new();
            foreach (var element in pathElements)
            {
                if (int.TryParse(element, out int index))
                {
                    indices.Add(index);
                }
            }

            return new GH_Path(indices.ToArray());
        }
    }
}
