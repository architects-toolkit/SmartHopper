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
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters;
using SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyHandlers;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    /// <summary>
    /// Modern, maintainable property management system that replaces the old PropertyManager.
    /// Provides clean separation of concerns between filtering, extraction, and application
    /// of properties for Grasshopper objects.
    /// </summary>
    public class PropertyManagerV2
    {
        private readonly PropertyFilter _filter;
        private readonly PropertyHandlerRegistry _handlerRegistry;

        /// <summary>
        /// Initializes a new instance of PropertyManagerV2 with the specified context.
        /// </summary>
        /// <param name="context">The serialization context that determines which properties to include.</param>
        public PropertyManagerV2(SerializationContext context = SerializationContext.Standard)
        {
            _filter = new PropertyFilter(context);
            _handlerRegistry = PropertyHandlerRegistry.Instance;
        }

        /// <summary>
        /// Initializes a new instance of PropertyManagerV2 with a custom filter.
        /// </summary>
        /// <param name="filter">The custom property filter to use.</param>
        private PropertyManagerV2(PropertyFilter filter)
        {
            this._filter = filter;
            this._handlerRegistry = PropertyHandlerRegistry.Instance;
        }

        /// <summary>
        /// Creates a PropertyManagerV2 with custom filtering rules.
        /// </summary>
        /// <param name="customRule">Custom property filtering rule.</param>
        /// <returns>New PropertyManagerV2 instance with custom rules.</returns>
        public static PropertyManagerV2 CreateCustom(PropertyFilterRule customRule)
        {
            var customFilter = PropertyFilter.CreateCustom(customRule);
            return new PropertyManagerV2(customFilter);
        }

        /// <summary>
        /// Determines if a property should be included for the given object.
        /// </summary>
        /// <param name="propertyName">Name of the property to check.</param>
        /// <param name="sourceObject">The object that owns the property.</param>
        /// <returns>True if the property should be included.</returns>
        public bool ShouldIncludeProperty(string propertyName, object sourceObject)
        {
            return _filter.ShouldIncludeProperty(propertyName, sourceObject);
        }

        /// <summary>
        /// Gets all properties that should be extracted from the given object.
        /// </summary>
        /// <param name="sourceObject">The object to get properties for.</param>
        /// <returns>List of property names to extract.</returns>
        public List<string> GetPropertiesToExtract(object sourceObject)
        {
            return _filter.GetPropertiesToExtract(sourceObject);
        }

        /// <summary>
        /// Extracts all allowed properties from an object, avoiding redundant data.
        /// </summary>
        /// <param name="sourceObject">The object to extract properties from.</param>
        /// <returns>Dictionary of property names and their ComponentProperty wrappers.</returns>
        public Dictionary<string, ComponentProperty> ExtractProperties(object sourceObject)
        {
            var propertiesToExtract = GetPropertiesToExtract(sourceObject);
            var extractedValues = _handlerRegistry.ExtractProperties(sourceObject, propertiesToExtract);
            
            var result = new Dictionary<string, ComponentProperty>();
            
            foreach (var kvp in extractedValues)
            {
                if (ShouldIncludeProperty(kvp.Key, sourceObject))
                {
                    result[kvp.Key] = new ComponentProperty { Value = kvp.Value };
                }
            }

            // Remove redundant properties based on documentation
            RemoveIrrelevantProperties(result, sourceObject);

            return result;
        }

        /// <summary>
        /// Extracts a specific property from an object.
        /// </summary>
        /// <param name="sourceObject">The object to extract from.</param>
        /// <param name="propertyName">The property to extract.</param>
        /// <returns>The extracted ComponentProperty, or null if not allowed or extraction fails.</returns>
        public ComponentProperty ExtractProperty(object sourceObject, string propertyName)
        {
            if (!ShouldIncludeProperty(propertyName, sourceObject))
            {
                return null;
            }

            var value = _handlerRegistry.ExtractProperty(sourceObject, propertyName);
            return value != null ? new ComponentProperty { Value = value } : null;
        }

        /// <summary>
        /// Applies properties to a target object.
        /// </summary>
        /// <param name="targetObject">The object to apply properties to.</param>
        /// <param name="properties">Dictionary of property names and ComponentProperty values.</param>
        /// <returns>Dictionary indicating success/failure for each property.</returns>
        public Dictionary<string, bool> ApplyProperties(object targetObject, Dictionary<string, ComponentProperty> properties)
        {
            var valueDictionary = properties.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value?.Value);

            return _handlerRegistry.ApplyProperties(targetObject, valueDictionary);
        }

        /// <summary>
        /// Applies a single property to a target object.
        /// </summary>
        /// <param name="targetObject">The object to apply the property to.</param>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="propertyValue">The ComponentProperty containing the value.</param>
        /// <returns>True if the property was successfully applied.</returns>
        public bool ApplyProperty(object targetObject, string propertyName, ComponentProperty propertyValue)
        {
            return _handlerRegistry.ApplyProperty(targetObject, propertyName, propertyValue?.Value);
        }

        /// <summary>
        /// Gets related properties that should be extracted along with a primary property.
        /// </summary>
        /// <param name="sourceObject">The source object.</param>
        /// <param name="propertyName">The primary property name.</param>
        /// <returns>Additional property names to extract.</returns>
        public IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName)
        {
            return _handlerRegistry.GetRelatedProperties(sourceObject, propertyName);
        }

        /// <summary>
        /// Filters an existing property dictionary based on the current filter rules.
        /// </summary>
        /// <param name="properties">Properties to filter.</param>
        /// <param name="sourceObject">The object that owns these properties.</param>
        /// <returns>Filtered properties dictionary.</returns>
        public Dictionary<string, ComponentProperty> FilterProperties(
            Dictionary<string, ComponentProperty> properties, 
            object sourceObject)
        {
            return properties.FilterProperties(_filter, sourceObject);
        }

        /// <summary>
        /// Creates a property extraction summary for debugging purposes.
        /// </summary>
        /// <param name="sourceObject">The object to analyze.</param>
        /// <returns>Summary of what properties would be extracted and why.</returns>
        public PropertyExtractionSummary CreateExtractionSummary(object sourceObject)
        {
            var allProperties = sourceObject.GetType().GetProperties().Select(p => p.Name).ToList();
            var allowedProperties = GetPropertiesToExtract(sourceObject);
            var excludedProperties = allProperties.Except(allowedProperties).ToList();

            return new PropertyExtractionSummary
            {
                ObjectType = sourceObject.GetType().Name,
                TotalProperties = allProperties.Count,
                AllowedProperties = allowedProperties,
                ExcludedProperties = excludedProperties,
                FilterContext = _filter.ToString()
            };
        }

        /// <summary>
        /// Removes irrelevant properties based on documentation and best practices.
        /// Eliminates default values, empty collections, and redundant data.
        /// </summary>
        /// <param name="properties">Properties dictionary to clean up.</param>
        /// <param name="sourceObject">Source object for context.</param>
        private void RemoveIrrelevantProperties(Dictionary<string, ComponentProperty> properties, object sourceObject)
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in properties)
            {
                var propertyName = kvp.Key;
                var value = kvp.Value.Value;

                // Remove properties with default/irrelevant values
                if (IsIrrelevantProperty(propertyName, value, sourceObject))
                {
                    keysToRemove.Add(propertyName);
                }
            }

            // Remove identified irrelevant properties
            foreach (var key in keysToRemove)
            {
                properties.Remove(key);
            }
        }

        /// <summary>
        /// Determines if a property is irrelevant based on its value and context.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="value">Value of the property.</param>
        /// <param name="sourceObject">Source object for context.</param>
        /// <returns>True if the property is irrelevant and should be excluded.</returns>
        private static bool IsIrrelevantProperty(string propertyName, object value, object sourceObject)
        {
            // Note: value is already unwrapped in RemoveIrrelevantProperties before calling this method

            // Remove boolean/int properties that are false/0 (default values)
            if ((value is bool boolValue && !boolValue) || (value is int intValue && intValue == 0))
            {
                return propertyName switch
                {
                    "Locked" => true,
                    "Simplify" => true,
                    "Reverse" => true,
                    "Hidden" => true,
                    "Invert" => true,
                    "Selected" => true,
                    "IsPrincipal" => true,
                    _ => false
                };
            }

            // Remove DataMapping when it's 0 (None - default value)
            // Handle both int (from JSON) and enum (from extraction)
            if (propertyName == "DataMapping")
            {
                int mappingValue = value switch
                {
                    int i => i,
                    System.Enum e => Convert.ToInt32(e),
                    _ => -1
                };
                
                if (mappingValue == 0)
                {
                    return true;
                }
            }

            // Remove NickName when it equals the component Name
            if (propertyName == "NickName" && sourceObject is IGH_ActiveObject ghObj)
            {
                return string.IsNullOrEmpty(value?.ToString()) || value.ToString() == ghObj.Name;
            }

            // Remove PersistentData for sliders when CurrentValue exists (redundant)
            // NOTE: Only remove for sliders, other components need PersistentData for proper deserialization
            if (propertyName == "PersistentData" && sourceObject is GH_NumberSlider)
            {
                return true; // CurrentValue contains the same information in a more compact format
            }

            return false;
        }
    }

    /// <summary>
    /// Summary of property extraction for debugging and analysis.
    /// </summary>
    public class PropertyExtractionSummary
    {
        public string ObjectType { get; set; }
        public int TotalProperties { get; set; }
        public List<string> AllowedProperties { get; set; } = new();
        public List<string> ExcludedProperties { get; set; } = new();
        public string FilterContext { get; set; }

        public override string ToString()
        {
            return $"Object: {ObjectType}, Total: {TotalProperties}, " +
                   $"Allowed: {AllowedProperties.Count}, Excluded: {ExcludedProperties.Count}";
        }
    }

    /// <summary>
    /// Factory class for creating PropertyManagerV2 instances with common configurations.
    /// </summary>
    public static class PropertyManagerFactory
    {
        /// <summary>
        /// Creates a PropertyManagerV2 with standard format (default).
        /// </summary>
        public static PropertyManagerV2 CreateStandard()
        {
            return new PropertyManagerV2(SerializationContext.Standard);
        }

        /// <summary>
        /// Creates a PropertyManagerV2 with lite format.
        /// </summary>
        public static PropertyManagerV2 CreateLite()
        {
            return new PropertyManagerV2(SerializationContext.Lite);
        }

        /// <summary>
        /// Creates a PropertyManagerV2 with custom component categories.
        /// </summary>
        /// <param name="includeCategories">Component categories to include.</param>
        public static PropertyManagerV2 CreateWithCategories(ComponentCategory includeCategories)
        {
            var customRule = new PropertyFilterRule
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = true,
                IncludeCategories = includeCategories
            };

            return PropertyManagerV2.CreateCustom(customRule);
        }
    }
}
