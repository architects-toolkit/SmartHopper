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

namespace SmartHopper.Core.JSON
{
    // JSON structure
    public class JsonStructure
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string ObjectType { get; set; }
        public Guid ComponentGuid { get; set; }
        public Guid ID { get; set; }
        public List<JsonInput> Inputs { get; set; }
        public List<JsonOutput> Outputs { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public bool Selected { get; set; }
        public List<object> Pivot { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }
    }

    public class JsonInput
    {
        public bool Undefined { get; set; } = false; //When other object == true
        public string InputName { get; set; } //When Type = component
        public List<Guid> Sources { get; set; } //When Type = component
        public string SourceName { get; set; } //When Type != component
        public List<Guid> SourceGuid { get; set; } //When Type != component
        public bool Simplify { get; set; } = false;
        public bool Reverse { get; set; } = false;
        public int DataMapping { get; set; }
        public string DataMappingHumanReadable { get; set; }
    }

    public class JsonOutput
    {
        public bool Undefined { get; set; } = false; //When other object
        public string OutputName { get; set; }  //When Type = component
        public List<Guid> Recipients { get; set; }  //When Type = component
        public string RecipientName { get; set; } //When Type != component
        public List<Guid> RecipientGuid { get; set; } //When Type != component
        public bool Simplify { get; set; } = false;
        public bool Reverse { get; set; } = false;
        public int DataMapping { get; set; }
        public string DataMappingHumanReadable { get; set; }
    }

    public class JsonProperties
    {
        private static Dictionary<string, List<string>> propertiesWhitelist = new Dictionary<string, List<string>>
        {
            {"Value", null},
            {"Locked", null}, // Enabled?
            {"Simplify", null}, //True or False
            {"Reverse", null}, //True or False
            {"DataMapping", null}, // Graft(2), Flatten(1) or None(0)
            {"DataType", null}, // remote(3), void(1) or local(2)
            {"Expression", null},
            {"Invert", null},
            {"NickName", null},
            {"DisplayName", null},
            // Internalized data in params
            {"PersistentData", null},
            // Current data
            {"VolatileData", null},
            // Panel
            {"UserText", null},
            {"Properties", new List<string> {"Properties"}}, // Only get Properties > Properties
            // Scribble
            {"Text", null},
            {"Font", null},
            {"Corners", null},
            // Number Slider, com fer even, odd, rational???
            {"CurrentValue", null},
            // {"InstanceDescription", null},
                // {"TickCount", null},
                // {"TickValue", null},
            // Control Knob
            {"Minimum", null},
            {"Maximum", null},
            {"Range", null},
            {"Decimals", null},
            {"Limit", null},
            {"DisplayFormat", null},
            // Multidimensional Slider
            {"SliderMode", null},
            {"XInterval", null},
            {"YInterval", null},
            {"ZInterval", null},
            {"X", null},
            {"Y", null},
            {"Z", null},
            // GeometryPipeline
            {"LayerFilter", null},
            {"NameFilter", null},
            {"TypeFilter", null},
            {"IncludeLocked", null},
            {"IncludeHidden", null},
            {"GroupByLayer", null},
            {"GroupByType", null},
            // GraphMapper
            {"GraphType", null},
            // PathMapper
            {"Lexers", null},
            // ValueList --> es podria netejar
            {"ListMode", null},
            {"ListItems", null},
            // ColorWheel
            {"State", null},
            // DataRecorder
            {"DataLimit", null},
            {"RecordData", null}, // Negatiu vol dir desactivat
            // ItemPicker (cherry picker)
            {"TreePath", null}, //Aquest valor no es defineix quan és {first}
            {"TreeIndex", null},
            // Button
            {"ExpressionNormal", null},
            {"ExpressionPressed", null},
        };
        public static bool IsPropertyInWhitelist(string propertyName)
        {
            return propertiesWhitelist.ContainsKey(propertyName);
        }
        public static bool HasChildProperties(string propertyName)
        {
            return propertiesWhitelist[propertyName] != null;
        }
        public static Dictionary<string, object> GetChildProperties(object value, string propertyName)
        {
            var childProperties = value.GetType().GetProperties();
            var childPropertyValues = new Dictionary<string, object>();

            foreach (var childProperty in childProperties)
            {
                if (propertiesWhitelist[propertyName].Contains(childProperty.Name))
                {
                    object childPropertyValue = childProperty.GetValue(value);
                    childPropertyValues[childProperty.Name] = childPropertyValue;
                }
            }

            return childPropertyValues;
        }
        //public static void AddChildPropertiesToDictionary(Dictionary<string, object> dictionary, string propertyName, Dictionary<string, object> childPropertyValues, object value)
        //{
        //    if (childPropertyValues.Count > 0)
        //    {
        //        dictionary[propertyName] = childPropertyValues;
        //    }
        //    else
        //    {
        //        dictionary[propertyName] = value;
        //    }
        //}
    }
}
