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

namespace SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyHandlers
{
    /// <summary>
    /// Defines the contract for handling property extraction and application
    /// for specific object types or property categories.
    /// </summary>
    public interface IPropertyHandler
    {
        /// <summary>
        /// Gets the priority of this handler. Higher priority handlers are tried first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Determines if this handler can process the given object and property.
        /// </summary>
        /// <param name="sourceObject">The object containing the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if this handler can process the property.</returns>
        bool CanHandle(object sourceObject, string propertyName);

        /// <summary>
        /// Extracts the property value from the source object.
        /// </summary>
        /// <param name="sourceObject">The object to extract from.</param>
        /// <param name="propertyName">The property to extract.</param>
        /// <returns>The extracted property value, or null if extraction fails.</returns>
        object ExtractProperty(object sourceObject, string propertyName);

        /// <summary>
        /// Applies the property value to the target object.
        /// </summary>
        /// <param name="targetObject">The object to apply the property to.</param>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if the property was successfully applied.</returns>
        bool ApplyProperty(object targetObject, string propertyName, object value);

        /// <summary>
        /// Gets additional properties that should be extracted when this property is encountered.
        /// For example, when extracting "CurrentValue" from a slider, also extract min/max values.
        /// </summary>
        /// <param name="sourceObject">The source object.</param>
        /// <param name="propertyName">The primary property name.</param>
        /// <returns>Additional property names to extract, or empty collection.</returns>
        IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName);
    }

    /// <summary>
    /// Base implementation of IPropertyHandler with common functionality.
    /// </summary>
    public abstract class PropertyHandlerBase : IPropertyHandler
    {
        public abstract int Priority { get; }

        public abstract bool CanHandle(object sourceObject, string propertyName);

        public virtual object ExtractProperty(object sourceObject, string propertyName)
        {
            try
            {
                var propertyInfo = sourceObject.GetType().GetProperty(propertyName);
                if (propertyInfo == null)
                {
                    return null;
                }

                var value = propertyInfo.GetValue(sourceObject);
                return ProcessExtractedValue(value, sourceObject, propertyName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting property {propertyName}: {ex.Message}");
                return null;
            }
        }

        public virtual bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            try
            {
                var propertyInfo = targetObject.GetType().GetProperty(propertyName);
                if (propertyInfo == null || !propertyInfo.CanWrite)
                {
                    return false;
                }

                var processedValue = ProcessValueForApplication(value, propertyInfo.PropertyType, targetObject, propertyName);
                propertyInfo.SetValue(targetObject, processedValue);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying property {propertyName}: {ex.Message}");
                return false;
            }
        }

        public virtual IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName)
        {
            return Array.Empty<string>();
        }

        /// <summary>
        /// Processes the extracted value before returning it.
        /// Override this to provide custom processing logic.
        /// </summary>
        protected virtual object ProcessExtractedValue(object value, object sourceObject, string propertyName)
        {
            return value;
        }

        /// <summary>
        /// Processes the value before applying it to the target property.
        /// Override this to provide custom conversion logic.
        /// </summary>
        protected virtual object ProcessValueForApplication(object value, Type targetType, object targetObject, string propertyName)
        {
            return ConvertValue(value, targetType);
        }

        /// <summary>
        /// Converts a value to the target type using common conversion patterns.
        /// </summary>
        protected static object ConvertValue(object value, Type targetType)
        {
            if (value == null || (value is JValue jValue && jValue.Value == null))
            {
                return null;
            }

            // Handle JValue unwrapping
            if (value is JValue jVal)
            {
                value = jVal.Value;
            }

            // Handle JObject for complex types
            if (value is JObject jObj)
            {
                return jObj.ToObject(targetType);
            }

            // If types already match
            if (value != null && targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // Handle nullable types
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (value == null) return null;
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            // Basic type conversions
            try
            {
                if (targetType == typeof(string))
                    return value?.ToString();

                if (targetType == typeof(int))
                    return Convert.ToInt32(value);

                if (targetType == typeof(double))
                    return Convert.ToDouble(value);

                if (targetType == typeof(float))
                    return Convert.ToSingle(value);

                if (targetType == typeof(bool))
                    return Convert.ToBoolean(value);

                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value.ToString());

                // Try direct conversion as fallback
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value; // Return original value if conversion fails
            }
        }
    }
}
