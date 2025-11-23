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
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyHandlers
{
    /// <summary>
    /// Handles PersistentData/VolatileData property for all parameter types.
    /// PersistentData: Parameter has no sources, data will be restored on deserialization.
    /// VolatileData: Parameter has sources, data is for AI context only and NOT restored.
    /// </summary>
    public class PersistentDataPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 100; // High priority for this critical property

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return (propertyName == "PersistentData" || propertyName == "VolatileData") && sourceObject is IGH_Param;
        }

        public override object ExtractProperty(object sourceObject, string propertyName)
        {
            if (sourceObject is IGH_Param param)
            {
                var dataTree = param.VolatileData;
                if (dataTree != null)
                {
                    var dictionary = DataTreeConverter.IGHStructureToDictionary(dataTree);
                    return DataTreeConverter.IGHStructureDictionaryTo1DDictionary(dictionary);
                }
            }
            return null;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            // Only apply PersistentData, skip VolatileData (it's for AI context only)
            if (propertyName == "VolatileData")
            {
                System.Diagnostics.Debug.WriteLine($"[PersistentDataPropertyHandler] Skipping VolatileData (for AI context only)");
                return true; // Return true to indicate it was "handled" (by skipping)
            }

            if (targetObject is IGH_DocumentObject docObj && value is JObject persistentDataObj)
            {
                System.Diagnostics.Debug.WriteLine($"[PersistentDataPropertyHandler] Applying PersistentData to {docObj.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[PersistentDataPropertyHandler] PersistentData JSON: {persistentDataObj}");
                SetPersistentData(docObj, persistentDataObj);

#if DEBUG
                // For debugging: check what was actually set
                if (docObj is IGH_Param param2)
                {
                    System.Diagnostics.Debug.WriteLine($"[PersistentDataPropertyHandler] VolatileData count: {param2.VolatileData?.DataCount ?? 0}");
                    if (param2.VolatileData != null && param2.VolatileData.DataCount > 0)
                    {
                        var firstItem = param2.VolatileData.AllData(true);
                        System.Diagnostics.Debug.WriteLine($"[PersistentDataPropertyHandler] First data item: {firstItem}");
                    }
                }
#endif

                return true;
            }
            return false;
        }

        private static void SetPersistentData(IGH_DocumentObject instance, JObject persistentDataDict)
        {
            // Extract values from nested structure
            var values = new List<JToken>();
            System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Processing {instance.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Input dict paths: {persistentDataDict.Count}");

            foreach (var path in persistentDataDict)
            {
                System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Path key: {path.Key}, Value type: {path.Value?.GetType().Name}");

                if (path.Value is JObject pathData)
                {
                    System.Diagnostics.Debug.WriteLine($"[SetPersistentData] PathData items: {pathData.Count}");
                    foreach (var item in pathData)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Item key: {item.Key}, Value: {item.Value}");

                        if (item.Value is JObject itemData && itemData.ContainsKey("value"))
                        {
                            values.Add(itemData["value"]);
                            System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Added value from itemData: {itemData["value"]}");
                        }
                        else
                        {
                            values.Add(item.Value);
                            System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Added item.Value directly: {item.Value}");
                        }
                    }
                }
            }

            var arrayData = new JArray(values);
            System.Diagnostics.Debug.WriteLine($"[SetPersistentData] Array data count: {arrayData.Count}, Content: {arrayData}");

            // Use type-specific conversion based on parameter type
            switch (instance)
            {
                case Param_Number paramNumber:
                    var pDataNumber = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        // Handle both prefixed format ("number:5.6") and direct double values
                        if (token.Type == JTokenType.String && DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object numResult) && numResult is double doubleValue)
                        {
                            return new GH_Number(doubleValue);
                        }
                        return new GH_Number(token.Value<double>());
                    });
                    paramNumber.SetPersistentData(pDataNumber);
                    break;

                case Param_Integer paramInt:
                    var pDataInt = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        // Handle both prefixed format ("integer:6") and direct int values
                        if (token.Type == JTokenType.String && DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object intResult) && intResult is int intValue)
                        {
                            return new GH_Integer(intValue);
                        }
                        return new GH_Integer(token.Value<int>());
                    });
                    paramInt.SetPersistentData(pDataInt);
                    break;

                case Param_String paramString:
                    var pDataString = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        // Handle both prefixed format ("text:Hello world!!") and direct string values
                        string stringValue = token.ToString();
                        if (DataTypeSerializer.TryDeserializeFromPrefix(stringValue, out object strResult) && strResult is string deserializedStr)
                        {
                            return new GH_String(deserializedStr);
                        }
                        return new GH_String(stringValue);
                    });
                    paramString.SetPersistentData(pDataString);
                    break;

                case Param_Boolean paramBoolean:
                    var pDataBoolean = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        // Handle both prefixed format ("boolean:true") and direct bool values
                        if (token.Type == JTokenType.String && DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object boolResult) && boolResult is bool boolValue)
                        {
                            return new GH_Boolean(boolValue);
                        }
                        return new GH_Boolean(token.Value<bool>());
                    });
                    paramBoolean.SetPersistentData(pDataBoolean);
                    break;

                // Geometric types using DataTypeSerializer
                case Param_Colour paramColour:
                    var pDataColour = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object colorResult)
                            && colorResult is Color color)
                        {
                            return new GH_Colour(color);
                        }
                        throw new InvalidOperationException($"Failed to deserialize color: {token}");
                    });
                    paramColour.SetPersistentData(pDataColour);
                    break;

                case Param_Point paramPoint:
                    var pDataPoint = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object pointResult)
                            && pointResult is Point3d point)
                        {
                            return new GH_Point(point);
                        }
                        throw new InvalidOperationException($"Failed to deserialize point: {token}");
                    });
                    paramPoint.SetPersistentData(pDataPoint);
                    break;

                case Param_Vector paramVector:
                    var pDataVector = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object vectorResult)
                            && vectorResult is Vector3d vector)
                        {
                            return new GH_Vector(vector);
                        }
                        throw new InvalidOperationException($"Failed to deserialize vector: {token}");
                    });
                    paramVector.SetPersistentData(pDataVector);
                    break;

                case Param_Line paramLine:
                    var pDataLine = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object lineResult)
                            && lineResult is Line line)
                        {
                            return new GH_Line(line);
                        }
                        throw new InvalidOperationException($"Failed to deserialize line: {token}");
                    });
                    paramLine.SetPersistentData(pDataLine);
                    break;

                case Param_Plane paramPlane:
                    var pDataPlane = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object planeResult)
                            && planeResult is Plane plane)
                        {
                            return new GH_Plane(plane);
                        }
                        throw new InvalidOperationException($"Failed to deserialize plane: {token}");
                    });
                    paramPlane.SetPersistentData(pDataPlane);
                    break;

                case Param_Arc paramArc:
                    var pDataArc = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object arcResult)
                            && arcResult is Arc arc)
                        {
                            return new GH_Arc(arc);
                        }
                        throw new InvalidOperationException($"Failed to deserialize arc: {token}");
                    });
                    paramArc.SetPersistentData(pDataArc);
                    break;

                case Param_Box paramBox:
                    var pDataBox = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object boxResult)
                            && boxResult is Box box)
                        {
                            return new GH_Box(box);
                        }
                        throw new InvalidOperationException($"Failed to deserialize box: {token}");
                    });
                    paramBox.SetPersistentData(pDataBox);
                    break;

                case Param_Circle paramCircle:
                    var pDataCircle = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object circleResult)
                            && circleResult is Circle circle)
                        {
                            return new GH_Circle(circle);
                        }
                        throw new InvalidOperationException($"Failed to deserialize circle: {token}");
                    });
                    paramCircle.SetPersistentData(pDataCircle);
                    break;

                case Param_Rectangle paramRectangle:
                    var pDataRectangle = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object rectangleResult)
                            && rectangleResult is Rectangle3d rectangle)
                        {
                            return new GH_Rectangle(rectangle);
                        }
                        throw new InvalidOperationException($"Failed to deserialize rectangle: {token}");
                    });
                    paramRectangle.SetPersistentData(pDataRectangle);
                    break;

                case Param_Interval paramInterval:
                    var pDataInterval = DataTreeConverter.JObjectToIGHStructure(arrayData, token =>
                    {
                        if (DataTypeSerializer.TryDeserializeFromPrefix(token.ToString(), out object intervalResult)
                            && intervalResult is Interval interval)
                        {
                            return new GH_Interval(interval);
                        }
                        throw new InvalidOperationException($"Failed to deserialize interval: {token}");
                    });
                    paramInterval.SetPersistentData(pDataInterval);
                    break;
            }
        }
    }


    /// <summary>
    /// Handles GH_Panel visualization properties that live under panel.Properties.
    /// Extracts and applies Alignment, Multiline, DrawIndices, DrawPaths, SpecialCodes.
    /// </summary>
    public class PanelPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 60;

        private static readonly HashSet<string> Supported = new HashSet<string>
        {
            "Alignment", "Multiline", "DrawIndices", "DrawPaths", "SpecialCodes"
        };

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return sourceObject is GH_Panel && Supported.Contains(propertyName);
        }

        public override object ExtractProperty(object sourceObject, string propertyName)
        {
            if (sourceObject is GH_Panel panel)
            {
                var props = panel.Properties;
                switch (propertyName)
                {
                    case "Alignment":
                        return props.Alignment.ToString();
                    case "Multiline":
                        return props.Multiline;
                    case "DrawIndices":
                        return props.DrawIndices;
                    case "DrawPaths":
                        return props.DrawPaths;
                    case "SpecialCodes":
                        return props.SpecialCodes;
                }
            }
            return null;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            if (targetObject is GH_Panel panel)
            {
                var props = panel.Properties;
                try
                {
                    switch (propertyName)
                    {
                        case "Alignment":
                            if (value != null)
                            {
                                var s = value.ToString();
                                if (Enum.TryParse(typeof(GH_Panel.Alignment), s, out var enumVal))
                                {
                                    props.Alignment = (GH_Panel.Alignment)enumVal;
                                    return true;
                                }
                            }
                            break;
                        case "Multiline":
                            props.Multiline = Convert.ToBoolean(value);
                            return true;
                        case "DrawIndices":
                            props.DrawIndices = Convert.ToBoolean(value);
                            return true;
                        case "DrawPaths":
                            props.DrawPaths = Convert.ToBoolean(value);
                            return true;
                        case "SpecialCodes":
                            props.SpecialCodes = Convert.ToBoolean(value);
                            return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PanelPropertyHandler] Failed to apply {propertyName}: {ex.Message}");
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Handles CurrentValue property for number sliders with special formatting.
    /// </summary>
    public class SliderCurrentValuePropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 90;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "CurrentValue" && sourceObject is GH_NumberSlider;
        }

        protected override object ProcessExtractedValue(object value, object sourceObject, string propertyName)
        {
            if (sourceObject is GH_NumberSlider slider)
            {
                // Format: "currentValue<min,max>"
                var currentValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                var min = slider.Slider.Minimum;
                var max = slider.Slider.Maximum;
                var decimals = Math.Max(0, slider.Slider.DecimalPlaces);
                var format = decimals == 0 ? "F0" : $"F{decimals}";

                // Only pad the VALUE with trailing zeros; keep min/max unpadded to compact the JSON
                return $"{currentValue.ToString(format, CultureInfo.InvariantCulture)}<{min.ToString(CultureInfo.InvariantCulture)},{max.ToString(CultureInfo.InvariantCulture)}>";
            }
            return value;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            if (targetObject is GH_NumberSlider slider && value is string formatted)
            {
                try
                {
                    // Parse format: "currentValue<min,max>"
                    var match = System.Text.RegularExpressions.Regex.Match(formatted, @"^(.+)<(.+),(.+)>$");
                    if (match.Success)
                    {
                        if (decimal.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var current) &&
                            decimal.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var min) &&
                            decimal.TryParse(match.Groups[3].Value, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var max))
                        {
                            var decimals = GetDecimalPlaces(match.Groups[1].Value);
                            slider.Slider.DecimalPlaces = Math.Max(0, decimals);
                            slider.Slider.Minimum = min;
                            slider.Slider.Maximum = max;
                            slider.SetSliderValue(current);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing slider value '{formatted}': {ex.Message}");
                }
            }

            return false;
        }

        private static int GetDecimalPlaces(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var idx = s.IndexOf('.', StringComparison.Ordinal);
            if (idx < 0) return 0;
            var end = s.IndexOfAny(new[] { 'e', 'E' }, idx + 1);
            var decimals = (end > idx ? end : s.Length) - idx - 1;
            return decimals < 0 ? 0 : decimals;
        }

        public override IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName)
        {
            // When extracting CurrentValue, also extract related slider properties
            return new[] { "Minimum", "Maximum", "Range", "Decimals", "Rounding" };
        }
    }

    /// <summary>
    /// Handles Rounding property for number sliders (R/N/E/O).
    /// Uses reflection to be resilient to API differences.
    /// </summary>
    public class SliderRoundingPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 85;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "Rounding" && sourceObject is GH_NumberSlider;
        }

        public override object ExtractProperty(object sourceObject, string propertyName)
        {
            try
            {
                if (sourceObject is GH_NumberSlider slider)
                {
                    var sliderCore = slider.Slider;
                    if (sliderCore == null) return null;

                    // Try known property names first
                    var coreType = sliderCore.GetType();
                    var roundingProp = coreType.GetProperty("Rounding") ?? coreType.GetProperty("Type");
                    if (roundingProp != null)
                    {
                        var enumVal = roundingProp.GetValue(sliderCore);
                        var name = enumVal?.ToString() ?? string.Empty;
                        return MapRoundingNameToCode(name);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SliderRoundingPropertyHandler] Extract error: {ex.Message}");
            }

            return null;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            try
            {
                if (targetObject is GH_NumberSlider slider && value is string code)
                {
                    var name = MapCodeToRoundingName(code);
                    var sliderCore = slider.Slider;
                    if (sliderCore == null) return false;
                    var coreType = sliderCore.GetType();

                    // Prefer enum property
                    var roundingProp = coreType.GetProperty("Rounding") ?? coreType.GetProperty("Type");
                    if (roundingProp != null && roundingProp.CanWrite)
                    {
                        var enumType = roundingProp.PropertyType;
                        // Try to find enum by name (Rational, Natural, Even, Odd)
                        var enumValue = Enum.GetNames(enumType)
                            .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                        if (enumValue != null)
                        {
                            var parsed = Enum.Parse(enumType, enumValue);
                            roundingProp.SetValue(sliderCore, parsed);
                            return true;
                        }
                    }

                    // Fallback to method invocations like SetRational/SetInteger/SetEven/SetOdd
                    var method = coreType.GetMethod($"Set{name}")
                                 ?? coreType.GetMethod($"Set{name.ToLowerInvariant().First().ToString().ToUpper()}{name.Substring(1).ToLowerInvariant()}");
                    if (method != null)
                    {
                        method.Invoke(sliderCore, null);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SliderRoundingPropertyHandler] Apply error: {ex.Message}");
            }
            return false;
        }

        private static string MapRoundingNameToCode(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            switch (name.Trim().ToLowerInvariant())
            {
                case "float": return "R";
                case "integer": return "N";
                case "even": return "E";
                case "odd": return "O";
                default: return null;
            }
        }

        private static string MapCodeToRoundingName(string code)
        {
            if (string.IsNullOrEmpty(code)) return "Float";
            switch (code.Trim().ToUpperInvariant())
            {
                case "R": return "Float";
                case "N": return "Integer";
                case "E": return "Even";
                case "O": return "Odd";
                default: return "Float";
            }
        }
    }

    /// <summary>
    /// Handles Expression property using reflection for parameters that support it.
    /// </summary>
    public class ExpressionPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 80;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "Expression" && sourceObject is IGH_Param;
        }

        public override object ExtractProperty(object sourceObject, string propertyName)
        {
            try
            {
                var expressionProperty = sourceObject.GetType().GetProperty("Expression");
                if (expressionProperty != null)
                {
                    return expressionProperty.GetValue(sourceObject) as string;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting expression from parameter: {ex.Message}");
            }

            return null;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            try
            {
                var expressionProperty = targetObject.GetType().GetProperty("Expression");
                if (expressionProperty != null && expressionProperty.CanWrite && value is string expression)
                {
                    expressionProperty.SetValue(targetObject, expression);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying expression to parameter: {ex.Message}");
            }

            return false;
        }
    }

    /// <summary>
    /// Handles Color properties with DataTypeSerializer support.
    /// </summary>
    public class ColorPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 70;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            var propertyInfo = sourceObject.GetType().GetProperty(propertyName);
            return propertyInfo?.PropertyType == typeof(Color);
        }

        protected override object ProcessValueForApplication(object value, Type targetType, object targetObject, string propertyName)
        {
            if (value is string stringValue)
            {
                // Try DataTypeSerializer first
                if (DataTypeSerializer.TryDeserialize("Color", stringValue, out object colorResult))
                {
                    return colorResult;
                }

                // Fallback to StringConverter
                return StringConverter.StringToColor(stringValue);
            }

            return base.ProcessValueForApplication(value, targetType, targetObject, propertyName);
        }
    }

    /// <summary>
    /// Handles Font properties.
    /// </summary>
    public class FontPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 70;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            var propertyInfo = sourceObject.GetType().GetProperty(propertyName);
            return propertyInfo?.PropertyType == typeof(Font);
        }

        protected override object ProcessValueForApplication(object value, Type targetType, object targetObject, string propertyName)
        {
            if (value is string stringValue)
            {
                return StringConverter.StringToFont(stringValue);
            }

            return base.ProcessValueForApplication(value, targetType, targetObject, propertyName);
        }
    }

    /// <summary>
    /// Handles GH_DataMapping enum properties.
    /// </summary>
    public class DataMappingPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 70;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            var propertyInfo = sourceObject.GetType().GetProperty(propertyName);
            return propertyInfo?.PropertyType == typeof(GH_DataMapping);
        }

        protected override object ProcessValueForApplication(object value, Type targetType, object targetObject, string propertyName)
        {
            return StringConverter.StringToGHDataMapping(value);
        }
    }

    /// <summary>
    /// Handles ListItems property for GH_ValueList with proper deserialization.
    /// </summary>
    public class ValueListItemsPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 95; // High priority for this specialized property

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "ListItems" && sourceObject is GH_ValueList;
        }

        public override object ExtractProperty(object sourceObject, string propertyName)
        {
            if (sourceObject is GH_ValueList valueList)
            {
                // Extract only essential properties to avoid serializing unnecessary UI data
                var simplifiedItems = new List<object>();

                foreach (var item in valueList.ListItems)
                {
                    simplifiedItems.Add(new
                    {
                        Name = item.Name,
                        Expression = item.Expression,
                        Selected = item.Selected
                    });
                }

                return simplifiedItems;
            }

            return null;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            System.Diagnostics.Debug.WriteLine($"[ValueListItemsPropertyHandler] ApplyProperty called: targetObject={targetObject?.GetType().Name}, propertyName={propertyName}, value type={value?.GetType().Name}");

            if (targetObject is GH_ValueList valueList && value != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[ValueListItemsPropertyHandler] Applying ListItems to ValueList");

                    // Handle both JArray directly and JObject with "value" property
                    JArray itemsArray = null;
                    if (value is JArray directArray)
                    {
                        itemsArray = directArray;
                    }
                    else if (value is JObject listItemsObj)
                    {
                        itemsArray = listItemsObj["value"] as JArray;
                    }

                    if (itemsArray != null)
                    {
                        // Clear default items
                        valueList.ListItems.Clear();
                        System.Diagnostics.Debug.WriteLine($"[ValueListItemsPropertyHandler] Cleared default items, adding {itemsArray.Count} custom items");

                        // Reconstruct each list item from simplified format
                        int firstSelectedIndex = -1;
                        int index = 0;
                        foreach (var itemToken in itemsArray)
                        {
                            var itemObj = itemToken as JObject;
                            if (itemObj != null)
                            {
                                var name = itemObj["Name"]?.ToString();
                                var expression = itemObj["Expression"]?.ToString();
                                var selected = itemObj["Selected"]?.Value<bool>() ?? false;

                                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(expression))
                                {
                                    var item = new GH_ValueListItem(name, expression);
                                    item.Selected = selected;
                                    valueList.ListItems.Add(item);
                                    if (selected && firstSelectedIndex == -1)
                                        firstSelectedIndex = index;
                                    index++;

                                    System.Diagnostics.Debug.WriteLine($"[ValueListItemsPropertyHandler] Added item: {item.Name} = {item.Expression}, Selected: {item.Selected}");
                                }
                            }
                        }

                        // Ensure consistent selection rules
                        // - If none selected, select the first item when available
                        // - If not CheckList and multiple selected, enforce single selection using SelectItem
                        bool anySelected = valueList.ListItems.Any(it => it.Selected);
                        if (!anySelected && valueList.ListItems.Count > 0)
                        {
                            valueList.SelectItem(0);
                        }
                        else if (anySelected && valueList.ListMode != GH_ValueListMode.CheckList)
                        {
                            int idxSel = firstSelectedIndex >= 0 ? firstSelectedIndex : valueList.ListItems.FindIndex(it => it.Selected);
                            if (idxSel < 0) idxSel = 0;
                            valueList.SelectItem(idxSel);
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ValueListItemsPropertyHandler] Error applying ListItems: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ValueListItemsPropertyHandler] Stack trace: {ex.StackTrace}");
                }
            }

            return false;
        }

        public override IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName)
        {
            // Also extract ListMode to preserve selection semantics
            return new[] { "ListMode" };
        }
    }

    /// <summary>
    /// Handles ListMode property for GH_ValueList to support selection semantics.
    /// </summary>
    public class ValueListModePropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 94; // Just below items

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "ListMode" && sourceObject is GH_ValueList;
        }

        public override object ExtractProperty(object sourceObject, string propertyName)
        {
            if (sourceObject is GH_ValueList valueList)
            {
                // Serialize as string name for stability
                return valueList.ListMode.ToString();
            }
            return null;
        }

        public override bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            System.Diagnostics.Debug.WriteLine($"[ValueListModePropertyHandler] ApplyProperty called: targetObject={targetObject?.GetType().Name}, propertyName={propertyName}, value={value}");

            if (targetObject is GH_ValueList valueList && value != null)
            {
                try
                {
                    // Accept either int or string
                    if (value is int i)
                    {
                        valueList.ListMode = (GH_ValueListMode)i;
                        return true;
                    }
                    var s = value.ToString();
                    if (Enum.TryParse(typeof(GH_ValueListMode), s, true, out var enumVal))
                    {
                        valueList.ListMode = (GH_ValueListMode)enumVal;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ValueListModePropertyHandler] Error applying ListMode: {ex.Message}");
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Default handler for standard properties that don't need special processing.
    /// </summary>
    public class DefaultPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 0; // Lowest priority - fallback handler

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            // Can handle any property that exists on the object
            return sourceObject.GetType().GetProperty(propertyName) != null;
        }
    }
}
