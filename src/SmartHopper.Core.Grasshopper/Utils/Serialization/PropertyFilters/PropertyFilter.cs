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

using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel.Special;
using RhinoCodePlatform.GH;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters
{
    /// <summary>
    /// Provides intelligent property filtering based on object type and serialization context.
    /// This replaces the old hardcoded whitelist/blacklist approach with a flexible,
    /// maintainable system.
    /// </summary>
    public class PropertyFilter
    {
        private readonly SerializationContext _context;
        private readonly PropertyFilterRule _rule;
        private readonly HashSet<string> _allowedProperties;

        public PropertyFilter(SerializationContext context = SerializationContext.Standard)
        {
            _context = context;
            _rule = PropertyFilterConfig.ContextRules[context];
            _allowedProperties = BuildAllowedPropertiesSet();
        }

        /// <summary>
        /// Determines if a property should be included in serialization for the given object.
        /// </summary>
        /// <param name="propertyName">Name of the property to check.</param>
        /// <param name="sourceObject">The object that owns the property.</param>
        /// <returns>True if the property should be included.</returns>
        public bool ShouldIncludeProperty(string propertyName, object sourceObject)
        {
            // Always exclude globally blacklisted properties
            if (PropertyFilterConfig.GlobalBlacklist.Contains(propertyName))
            {
                return false;
            }

            // Check context-specific additional excludes
            if (_rule.AdditionalExcludes.Contains(propertyName))
            {
                return false;
            }

            // Always include context-specific additional includes
            if (_rule.AdditionalIncludes.Contains(propertyName))
            {
                return true;
            }

            // Check if property is in the allowed set for this context
            if (_allowedProperties.Contains(propertyName))
            {
                return true;
            }

            // Check category-specific properties
            var category = GetComponentCategory(sourceObject);
            if (category != ComponentCategory.None && _rule.IncludeCategories.HasFlag(category))
            {
                if (PropertyFilterConfig.CategoryProperties.TryGetValue(category, out var categoryProps))
                {
                    return categoryProps.Contains(propertyName);
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all allowed properties for a specific object in the current context.
        /// </summary>
        /// <param name="sourceObject">The object to get properties for.</param>
        /// <returns>Set of allowed property names.</returns>
        public HashSet<string> GetAllowedProperties(object sourceObject)
        {
            var result = new HashSet<string>(_allowedProperties);

            // Add category-specific properties if applicable
            var category = GetComponentCategory(sourceObject);
            if (category != ComponentCategory.None && _rule.IncludeCategories.HasFlag(category))
            {
                if (PropertyFilterConfig.CategoryProperties.TryGetValue(category, out var categoryProps))
                {
                    result.UnionWith(categoryProps);
                }
            }

            // Remove globally blacklisted properties
            result.ExceptWith(PropertyFilterConfig.GlobalBlacklist);

            // Apply context-specific rules
            result.ExceptWith(_rule.AdditionalExcludes);
            result.UnionWith(_rule.AdditionalIncludes);

            return result;
        }

        /// <summary>
        /// Creates a custom filter with specific rules.
        /// </summary>
        /// <param name="customRule">Custom filtering rule.</param>
        /// <returns>New PropertyFilter instance.</returns>
        public static PropertyFilter CreateCustom(PropertyFilterRule customRule)
        {
            var filter = new PropertyFilter(SerializationContext.Standard);
            filter._rule.IncludeCore = customRule.IncludeCore;
            filter._rule.IncludeParameters = customRule.IncludeParameters;
            filter._rule.IncludeComponents = customRule.IncludeComponents;
            filter._rule.IncludeCategories = customRule.IncludeCategories;
            filter._rule.AdditionalIncludes.UnionWith(customRule.AdditionalIncludes);
            filter._rule.AdditionalExcludes.UnionWith(customRule.AdditionalExcludes);

            return filter;
        }

        /// <summary>
        /// Builds the base set of allowed properties based on the current rule.
        /// </summary>
        private HashSet<string> BuildAllowedPropertiesSet()
        {
            var properties = new HashSet<string>();

            if (_rule.IncludeCore)
            {
                properties.UnionWith(PropertyFilterConfig.CoreProperties);
            }

            if (_rule.IncludeParameters)
            {
                properties.UnionWith(PropertyFilterConfig.ParameterProperties);
            }

            if (_rule.IncludeComponents)
            {
                properties.UnionWith(PropertyFilterConfig.ComponentProperties);
            }

            return properties;
        }

        /// <summary>
        /// Determines the component category for category-specific property filtering.
        /// </summary>
        /// <param name="obj">The object to categorize.</param>
        /// <returns>The component category.</returns>
        private static ComponentCategory GetComponentCategory(object obj)
        {
            return obj switch
            {
                GH_Panel => ComponentCategory.Panel,
                GH_Scribble => ComponentCategory.Scribble,
                GH_NumberSlider => ComponentCategory.Slider,
                GH_MultiDimensionalSlider => ComponentCategory.MultidimensionalSlider,
                GH_ValueList => ComponentCategory.ValueList,
                GH_ButtonObject => ComponentCategory.Button,
                GH_BooleanToggle => ComponentCategory.BooleanToggle,
                GH_ColourSwatch => ComponentCategory.ColourSwatch,
                IScriptComponent => ComponentCategory.Script,
                GH_GeometryPipeline => ComponentCategory.GeometryPipeline,
                GH_GraphMapper => ComponentCategory.GraphMapper,
                GH_PathMapper => ComponentCategory.PathMapper,
                GH_ColourWheel => ComponentCategory.ColorWheel,
                GH_DataRecorder => ComponentCategory.DataRecorder,

                //GH_ItemSelector => ComponentCategory.ItemPicker,
                _ => ComponentCategory.None
            };
        }
    }

    /// <summary>
    /// Extension methods for easier property filtering.
    /// </summary>
    public static class PropertyFilterExtensions
    {
        /// <summary>
        /// Filters a dictionary of properties based on the given filter and source object.
        /// </summary>
        /// <param name="properties">Properties to filter.</param>
        /// <param name="filter">The property filter to use.</param>
        /// <param name="sourceObject">The object that owns these properties.</param>
        /// <returns>Filtered properties dictionary.</returns>
        public static Dictionary<string, T> FilterProperties<T>(
            this Dictionary<string, T> properties,
            PropertyFilter filter,
            object sourceObject)
        {
            return properties
                .Where(kvp => filter.ShouldIncludeProperty(kvp.Key, sourceObject))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Gets a list of property names that should be extracted for the given object.
        /// </summary>
        /// <param name="filter">The property filter.</param>
        /// <param name="sourceObject">The object to get properties for.</param>
        /// <returns>List of property names to extract.</returns>
        public static List<string> GetPropertiesToExtract(this PropertyFilter filter, object sourceObject)
        {
            return filter.GetAllowedProperties(sourceObject).ToList();
        }
    }
}
