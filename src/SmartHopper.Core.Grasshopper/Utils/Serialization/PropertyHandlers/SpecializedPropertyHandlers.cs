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
    /// Handles PersistentData property for all parameter types.
    /// </summary>
    public class PersistentDataPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 100; // High priority for this critical property

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "PersistentData" && sourceObject is IGH_Param;
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
            if (targetObject is IGH_DocumentObject docObj && value is JObject persistentDataObj)
            {
                SetPersistentData(docObj, persistentDataObj);
                return true;
            }
            return false;
        }

        private static void SetPersistentData(IGH_DocumentObject instance, JObject persistentDataDict)
        {
            // Extract values from nested structure
            var values = new List<JToken>();
            foreach (var path in persistentDataDict)
            {
                if (path.Value is JObject pathData)
                {
                    foreach (var item in pathData)
                    {
                        if (item.Value is JObject itemData && itemData.ContainsKey("value"))
                        {
                            values.Add(itemData["value"]);
                        }
                        else
                        {
                            values.Add(item.Value);
                        }
                    }
                }
            }

            var arrayData = new JArray(values);

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
                var instanceDesc = slider.InstanceDescription;
                var (accuracy, lowerLimit, upperLimit) = NumberSliderUtils.ParseInstanceDescription(instanceDesc);
                var currentValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return NumberSliderUtils.FormatSliderValue(lowerLimit, upperLimit, currentValue);
            }
            return value;
        }

        public override IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName)
        {
            // When extracting CurrentValue, also extract related slider properties
            return new[] { "Minimum", "Maximum", "Range", "Decimals" };
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
    /// Handles Panel Alignment enum properties.
    /// </summary>
    public class AlignmentPropertyHandler : PropertyHandlerBase
    {
        public override int Priority => 70;

        public override bool CanHandle(object sourceObject, string propertyName)
        {
            return propertyName == "Alignment" && sourceObject is GH_Panel;
        }

        protected override object ProcessValueForApplication(object value, Type targetType, object targetObject, string propertyName)
        {
            if (value is int alignmentValue)
            {
                return alignmentValue switch
                {
                    0 => GH_Panel.Alignment.Default,
                    1 => GH_Panel.Alignment.Left,
                    2 => GH_Panel.Alignment.Center,
                    3 => GH_Panel.Alignment.Right,
                    _ => GH_Panel.Alignment.Default
                };
            }

            return base.ProcessValueForApplication(value, targetType, targetObject, propertyName);
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
