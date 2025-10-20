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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    public static class PropertyManager
    {
        // List of omitted properties
        private static List<string> omittedProperties = new ()
        {
            "VolatileData",
            "DataType",
            "Properties",
        };

        // List of properties to serialize
        private static Dictionary<string, List<string>> PropertiesWhitelist { get; } = new Dictionary<string, List<string>>
        {
            { "Value", null },
            { "Locked", null }, // Enabled?
            { "Simplify", null }, // True or False
            { "Reverse", null }, // True or False
            { "DataMapping", null }, // Graft(2), Flatten(1) or None(0)
            { "DataType", null }, // remote(3), void(1) or local(2)
            { "Expression", null },
            { "Invert", null },
            { "NickName", null },
            { "DisplayName", null },

            // Internalized data in params
            { "PersistentData", null },

            // Current data
            { "VolatileData", null },

            // Panel
            { "UserText", null },
            { "Properties", new List<string> { "Properties" } }, // Only get Properties > Properties

            // Scribble
            { "Text", null },
            { "Font", null },
            { "Corners", null },

            // Number Slider, com fer even, odd, rational???
            { "CurrentValue", null },

            // {"InstanceDescription", null},
                // {"TickCount", null},
                // {"TickValue", null},
            // Control Knob
            { "Minimum", null },
            { "Maximum", null },
            { "Range", null },
            { "Decimals", null },
            { "Limit", null },
            { "DisplayFormat", null },

            // Multidimensional Slider
            { "SliderMode", null },
            { "XInterval", null },
            { "YInterval", null },
            { "ZInterval", null },
            { "X", null },
            { "Y", null },
            { "Z", null },

            // GeometryPipeline
            { "LayerFilter", null },
            { "NameFilter", null },
            { "TypeFilter", null },
            { "IncludeLocked", null },
            { "IncludeHidden", null },
            { "GroupByLayer", null },
            { "GroupByType", null },

            // GraphMapper
            { "GraphType", null },

            // PathMapper
            { "Lexers", null },

            // ValueList --> es podria netejar
            { "ListMode", null },
            { "ListItems", null },

            // ColorWheel
            { "State", null },

            // DataRecorder
            { "DataLimit", null },
            { "RecordData", null }, // Negatiu vol dir desactivat

            // ItemPicker (cherry picker)
            { "TreePath", null }, // Aquest valor no es defineix quan és {first}
            { "TreeIndex", null },

            // Button
            { "ExpressionNormal", null },
            { "ExpressionPressed", null },

            // Script
            { "Script", null },
        };

        public static bool IsPropertyInWhitelist(string propertyName)
        {
            return PropertiesWhitelist.ContainsKey(propertyName);
        }

        /// <summary>
        /// Returns the child-property names for a whitelisted property,
        /// or null if there are no nested keys.
        /// </summary>
        /// <returns></returns>
        public static List<string>? GetChildProperties(string propertyName)
        {
            if (PropertiesWhitelist.TryGetValue(propertyName, out var childKeys))
            {
                return childKeys;
            }

            return null;
        }

        public static void SetProperties(object instance, Dictionary<string, ComponentProperty> properties)
        {
            foreach (var prop in properties)
            {
                try
                {
                    Debug.WriteLine($"Setting property {prop.Key}...");
                    if (prop.Value != null)
                    {
                        SetProperty(instance, prop.Key, prop.Value.Value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting property {prop.Key}: {ex.Message}");
                }
            }
        }

        public static void SetProperty(object obj, string propertyPath, object value)
        {
            try
            {
                // Handle ComponentProperty wrapper
                if (value is JObject jObj && jObj.ContainsKey("value"))
                {
                    value = jObj["value"];
                }

                string[] parts = propertyPath.Split('.');
                object currentObj = obj;

                Debug.WriteLine($"Property path: {propertyPath}");
                Debug.WriteLine($"Trying to set property '{propertyPath}' value to '{value}' on object '{obj.GetType().Name}'");

                // Special handling for PersistentData
                if (propertyPath == "PersistentData" && obj is IGH_DocumentObject docObj && value is JObject persistentDataObj)
                {
                    SetPersistentData(docObj, persistentDataObj);
                    return;
                }

                // Navigate through the property path
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    PropertyInfo property = currentObj.GetType().GetProperty(parts[i]);
                    if (property == null)
                    {
                        Debug.WriteLine($"Error: Property '{parts[i]}' not found.");
                        return;
                    }

                    currentObj = property.GetValue(currentObj);
                    if (currentObj == null)
                    {
                        Debug.WriteLine($"Error: Property '{parts[i]}' value is null.");
                        return;
                    }
                }

                // Set the final property value
                PropertyInfo finalProperty = currentObj.GetType().GetProperty(parts[parts.Length - 1]);
                if (finalProperty != null)
                {
                    try
                    {
                        object convertedValue = ConvertValue(value, finalProperty.PropertyType);
                        finalProperty.SetValue(currentObj, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error setting property value: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Error: Final property '{parts[parts.Length - 1]}' not found.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SetProperty: {ex.Message}");
            }
        }

        private static object? ConvertValue(object value, Type targetType)
        {
            if (value == null || (value is JValue jValue && jValue.Value == null))
            {
                return null;
            }

            try
            {
                // Handle JValue
                if (value is JValue)
                {
                    value = ((JValue)value).Value;
                }

                // Handle JObject for complex types
                if (value is JObject jObj)
                {
                    // If the target type has a constructor that takes a dictionary
                    if (targetType.GetConstructor(new[] { typeof(IDictionary<string, object>) }) != null)
                    {
                        var dict = jObj.ToObject<Dictionary<string, object>>();
                        return Activator.CreateInstance(targetType, dict);
                    }

                    // Otherwise try to deserialize directly
                    return jObj.ToObject(targetType);
                }

                // Convert Int64 to Int32 if needed
                if (value.GetType() == typeof(long))
                {
                    value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }

                // If types match, return as is
                if (targetType == value.GetType())
                {
                    return value;
                }

                // Handle Grasshopper-specific types
                switch (targetType.Name)
                {
                    case "Color":
                        if (value is string stringValue)
                        {
                            // Try DataTypeSerializer first (comma-separated format)
                            if (DataTypeSerializer.TryDeserialize("Color", stringValue, out object colorResult))
                            {
                                Debug.WriteLine($"Color deserialized via DataTypeSerializer: {colorResult}");
                                return colorResult;
                            }

                            // Fallback to StringConverter for HTML/named colors
                            Color color = StringConverter.StringToColor(stringValue);
                            Debug.WriteLine($"Color converted via StringConverter: {color}");
                            return color;
                        }

                        break;

                    case "Font":
                        return StringConverter.StringToFont(value.ToString());

                    case "Alignment":
                        int alignmentValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        switch (alignmentValue)
                        {
                            case 0: return GH_Panel.Alignment.Default;
                            case 1: return GH_Panel.Alignment.Left;
                            case 2: return GH_Panel.Alignment.Center;
                            case 3: return GH_Panel.Alignment.Right;
                            default:
                                Debug.WriteLine($"Unknown Alignment value: {alignmentValue}");
                                return GH_Panel.Alignment.Default;
                        }

                    case "GH_DataMapping":
                        return StringConverter.StringToGHDataMapping(value);
                }

                // Handle basic type conversions
                if (targetType == typeof(string))
                {
                    return value?.ToString();
                }
                else if (targetType == typeof(int))
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                else if (targetType == typeof(double))
                {
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                }
                else if (targetType == typeof(bool))
                {
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                }
                else if (targetType == typeof(float))
                {
                    return Convert.ToSingle(value, CultureInfo.InvariantCulture);
                }
                else if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, value.ToString());
                }

                // If no specific conversion is defined, try direct assignment
                return value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting value: {ex.Message}");
                return value;
            }
        }

        public static bool IsPropertyOmitted(string propertyName)
        {
            // Check direct property names
            if (omittedProperties.Contains(propertyName))
            {
                return true;
            }

            // Check nested properties (Properties.X format)
            return omittedProperties
                .Where(op => op.StartsWith("Properties."))
                .Select(op => op.Split('.')[1])
                .Contains(propertyName);
        }

        public static void SetPersistentData(IGH_DocumentObject instance, JObject persistentDataDict)
        {
            // Extract values from nested structure
            var values = new List<JToken>();
            foreach (var path in persistentDataDict)
            {
                if (path.Value is JObject pathData)
                {
                    foreach (var item in pathData)
                    {
                        if (item.Value is JObject itemData)
                        {
                            values.Add(itemData["value"]);
                        }
                        else
                        {
                            // Handle direct string values (serialized data types)
                            values.Add(item.Value);
                        }
                    }
                }
            }

            // Create a simple array structure
            var arrayData = new JArray(values);

            switch (instance)
            {
                case Param_Number paramNumber:
                    Func<JToken, GH_Number> convertFunctionToNumber = (token) => new GH_Number(token.Value<float>());
                    var pDataNumber = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToNumber);
                    paramNumber.SetPersistentData(pDataNumber);
                    break;

                case Param_Integer paramInt:
                    Func<JToken, GH_Integer> convertFunctionToInt = (token) => new GH_Integer(token.Value<int>());
                    var pDataInt = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToInt);
                    paramInt.SetPersistentData(pDataInt);
                    break;

                case Param_String paramString:
                    Func<JToken, GH_String> convertFunctionToString = (token) => new GH_String(token.ToString());
                    var pDataString = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToString);
                    paramString.SetPersistentData(pDataString);
                    break;

                case Param_Boolean paramBoolean:
                    Func<JToken, GH_Boolean> convertFunctionToBoolean = (token) => new GH_Boolean(token.Value<bool>());
                    var pDataBoolean = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToBoolean);
                    paramBoolean.SetPersistentData(pDataBoolean);
                    break;

                // Geometric data types - fail explicitly if deserialization fails
                case Param_Colour paramColour:
                    Func<JToken, GH_Colour> convertFunctionToColour = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object colorResult) && colorResult is System.Drawing.Color color)
                        {
                            return new GH_Colour(color);
                        }
                        throw new InvalidOperationException($"Failed to deserialize color: {valueStr}");
                    };
                    var pDataColour = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToColour);
                    paramColour.SetPersistentData(pDataColour);
                    break;

                case Param_Point paramPoint:
                    Func<JToken, GH_Point> convertFunctionToPoint = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object pointResult) && pointResult is Point3d point)
                        {
                            return new GH_Point(point);
                        }
                        throw new InvalidOperationException($"Failed to deserialize point: {valueStr}");
                    };
                    var pDataPoint = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToPoint);
                    paramPoint.SetPersistentData(pDataPoint);
                    break;

                case Param_Vector paramVector:
                    Func<JToken, GH_Vector> convertFunctionToVector = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object vectorResult) && vectorResult is Vector3d vector)
                        {
                            return new GH_Vector(vector);
                        }
                        throw new InvalidOperationException($"Failed to deserialize vector: {valueStr}");
                    };
                    var pDataVector = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToVector);
                    paramVector.SetPersistentData(pDataVector);
                    break;

                case Param_Line paramLine:
                    Func<JToken, GH_Line> convertFunctionToLine = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object lineResult) && lineResult is Line line)
                        {
                            return new GH_Line(line);
                        }
                        throw new InvalidOperationException($"Failed to deserialize line: {valueStr}");
                    };
                    var pDataLine = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToLine);
                    paramLine.SetPersistentData(pDataLine);
                    break;

                case Param_Plane paramPlane:
                    Func<JToken, GH_Plane> convertFunctionToPlane = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object planeResult) && planeResult is Plane plane)
                        {
                            return new GH_Plane(plane);
                        }
                        throw new InvalidOperationException($"Failed to deserialize plane: {valueStr}");
                    };
                    var pDataPlane = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToPlane);
                    paramPlane.SetPersistentData(pDataPlane);
                    break;

                case Param_Circle paramCircle:
                    Func<JToken, GH_Circle> convertFunctionToCircle = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object circleResult) && circleResult is Circle circle)
                        {
                            return new GH_Circle(circle);
                        }
                        throw new InvalidOperationException($"Failed to deserialize circle: {valueStr}");
                    };
                    var pDataCircle = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToCircle);
                    paramCircle.SetPersistentData(pDataCircle);
                    break;

                case Param_Arc paramArc:
                    Func<JToken, GH_Arc> convertFunctionToArc = (token) => 
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object arcResult) && arcResult is Arc arc)
                        {
                            return new GH_Arc(arc);
                        }
                        throw new InvalidOperationException($"Failed to deserialize arc: {valueStr}");
                    };
                    var pDataArc = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToArc);
                    paramArc.SetPersistentData(pDataArc);
                    break;

                case Param_Box paramBox:
                    Func<JToken, GH_Box> convertFunctionToBox = (token) =>
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object boxResult) && boxResult is Box box)
                        {
                            return new GH_Box(box);
                        }
                        throw new InvalidOperationException($"Failed to deserialize box: {valueStr}");
                    };
                    var pDataBox = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToBox);
                    paramBox.SetPersistentData(pDataBox);
                    break;

                case Param_Rectangle paramRectangle:
                    Func<JToken, GH_Rectangle> convertFunctionToRectangle = (token) =>
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object rectangleResult) && rectangleResult is Rectangle3d rectangle)
                        {
                            return new GH_Rectangle(rectangle);
                        }
                        throw new InvalidOperationException($"Failed to deserialize rectangle: {valueStr}");
                    };
                    var pDataRectangle = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToRectangle);
                    paramRectangle.SetPersistentData(pDataRectangle);
                    break;

                case Param_Interval paramInterval:
                    Func<JToken, GH_Interval> convertFunctionToInterval = (token) =>
                    {
                        string valueStr = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(valueStr, out object intervalResult) && intervalResult is Interval interval)
                        {
                            return new GH_Interval(interval);
                        }
                        throw new InvalidOperationException($"Failed to deserialize interval: {valueStr}");
                    };
                    var pDataInterval = DataTreeConverter.JObjectToIGHStructure(arrayData, convertFunctionToInterval);
                    paramInterval.SetPersistentData(pDataInterval);
                    break;

                default:
                    Debug.WriteLine($"No handling implemented for type {instance.GetType().Name}"); 
                    break;
            }
        }
    }
}
