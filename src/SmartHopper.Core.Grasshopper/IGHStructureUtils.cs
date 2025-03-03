/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SmartHopper.Core.Grasshopper
{

    public static class IGHStructureProcessor
    {
        public static Dictionary<string, List<object>> IGHStructureToDictionary(IGH_Structure structure)
        {
            Dictionary<string, List<object>> result = new Dictionary<string, List<object>>();

            foreach (GH_Path path in structure.Paths)
            {
                List<object> dataList = new List<object>();

                // Iterate over the data items in the path
                foreach (object dataItem in structure.get_Branch(path))
                {
                    // Add the data item to the list
                    dataList.Add(dataItem);
                    //Debug.WriteLine($"{path.ToString()}: {dataItem.ToString()}");
                }

                // Add the path and list to the dictionary
                result.Add(path.ToString(), dataList);
            }

            return result;
        }

        public static Dictionary<string, object> IGHStructureDictionaryTo1DDictionary(Dictionary<string, List<object>> dictionary)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

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

        public static GH_Structure<T> JObjectToIGHStructure<T>(JToken input, Func<JToken, T> convertFunction) where T : IGH_Goo
        {
            GH_Structure<T> result = new GH_Structure<T>();

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
                    GH_Path p = new GH_Path(ParseKeyToPath(path.Key));
                    JObject items = (JObject)path.Value;

                    foreach (var item in items)
                    {
                        foreach (var property in item.Value as JObject)
                        {
                            if (property.Key == "Value")
                            {
                                result.Append(convertFunction(property.Value), p);
                                Debug.WriteLine($"{p} value found to be: {property.Value}");
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
            string cleanedKey = Regex.Replace(key, @"\(\d+\)", "");
            var pathElements = cleanedKey.Trim('{', '}').Split(';');

            // Convert the path elements to integers and create a new GH_Path
            List<int> indices = new List<int>();
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
