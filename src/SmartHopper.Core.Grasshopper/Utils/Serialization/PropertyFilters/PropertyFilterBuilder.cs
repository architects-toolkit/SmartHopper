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

namespace SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters
{
    /// <summary>
    /// Fluent builder for creating custom property filter rules.
    /// Provides an easy-to-use API for configuring complex filtering scenarios.
    /// </summary>
    public class PropertyFilterBuilder
    {
        private readonly PropertyFilterRule _rule;

        private PropertyFilterBuilder()
        {
            _rule = new PropertyFilterRule();
        }

        /// <summary>
        /// Creates a new PropertyFilterBuilder instance.
        /// </summary>
        /// <returns>New builder instance.</returns>
        public static PropertyFilterBuilder Create()
        {
            return new PropertyFilterBuilder();
        }

        /// <summary>
        /// Starts with a predefined context as the base configuration.
        /// </summary>
        /// <param name="context">Base serialization context.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public static PropertyFilterBuilder FromContext(SerializationContext context)
        {
            var builder = new PropertyFilterBuilder();
            var baseRule = PropertyFilterConfig.ContextRules[context];

            builder._rule.IncludeCore = baseRule.IncludeCore;
            builder._rule.IncludeParameters = baseRule.IncludeParameters;
            builder._rule.IncludeComponents = baseRule.IncludeComponents;
            builder._rule.IncludeCategories = baseRule.IncludeCategories;
            builder._rule.AdditionalIncludes.UnionWith(baseRule.AdditionalIncludes);
            builder._rule.AdditionalExcludes.UnionWith(baseRule.AdditionalExcludes);

            return builder;
        }

        /// <summary>
        /// Includes or excludes core properties (NickName, Locked, PersistentData).
        /// </summary>
        /// <param name="include">Whether to include core properties.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder WithCore(bool include = true)
        {
            _rule.IncludeCore = include;
            return this;
        }

        /// <summary>
        /// Includes or excludes parameter-specific properties.
        /// </summary>
        /// <param name="include">Whether to include parameter properties.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder WithParameters(bool include = true)
        {
            _rule.IncludeParameters = include;
            return this;
        }

        /// <summary>
        /// Includes or excludes component-specific properties.
        /// </summary>
        /// <param name="include">Whether to include component properties.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder WithComponents(bool include = true)
        {
            _rule.IncludeComponents = include;
            return this;
        }

        /// <summary>
        /// Specifies which component categories to include.
        /// </summary>
        /// <param name="categories">Component categories to include.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder WithCategories(ComponentCategory categories)
        {
            _rule.IncludeCategories = categories;
            return this;
        }

        /// <summary>
        /// Adds specific component categories to the existing set.
        /// </summary>
        /// <param name="categories">Additional categories to include.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder AddCategories(ComponentCategory categories)
        {
            _rule.IncludeCategories |= categories;
            return this;
        }

        /// <summary>
        /// Removes specific component categories from the existing set.
        /// </summary>
        /// <param name="categories">Categories to exclude.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder RemoveCategories(ComponentCategory categories)
        {
            _rule.IncludeCategories &= ~categories;
            return this;
        }

        /// <summary>
        /// Includes only essential component categories (Panel, Scribble, Slider, ValueList, Script).
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder WithEssentialCategories()
        {
            return WithCategories(ComponentCategory.Essential);
        }

        /// <summary>
        /// Includes only UI-related component categories (Panel, Scribble, Button, ColorWheel).
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder WithUICategories()
        {
            return WithCategories(ComponentCategory.UI);
        }


        /// <summary>
        /// Adds specific properties to the include list (these will always be included).
        /// </summary>
        /// <param name="propertyNames">Property names to always include.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Include(params string[] propertyNames)
        {
            foreach (var property in propertyNames)
            {
                _rule.AdditionalIncludes.Add(property);
            }
            return this;
        }

        /// <summary>
        /// Adds specific properties to the exclude list (these will never be included).
        /// </summary>
        /// <param name="propertyNames">Property names to always exclude.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Exclude(params string[] propertyNames)
        {
            foreach (var property in propertyNames)
            {
                _rule.AdditionalExcludes.Add(property);
            }
            return this;
        }

        /// <summary>
        /// Adds multiple properties to the include list.
        /// </summary>
        /// <param name="propertyNames">Collection of property names to include.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Include(IEnumerable<string> propertyNames)
        {
            _rule.AdditionalIncludes.UnionWith(propertyNames);
            return this;
        }

        /// <summary>
        /// Adds multiple properties to the exclude list.
        /// </summary>
        /// <param name="propertyNames">Collection of property names to exclude.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Exclude(IEnumerable<string> propertyNames)
        {
            _rule.AdditionalExcludes.UnionWith(propertyNames);
            return this;
        }

        /// <summary>
        /// Configures the builder for minimal serialization (only core properties).
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Minimal()
        {
            return WithCore(true)
                   .WithParameters(false)
                   .WithComponents(false)
                   .WithCategories(ComponentCategory.None);
        }

        /// <summary>
        /// Configures the builder for maximum serialization (all properties).
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Maximum()
        {
            return WithCore(true)
                   .WithParameters(true)
                   .WithComponents(true)
                   .WithCategories(ComponentCategory.All);
        }

        /// <summary>
        /// Configures the builder for slider-specific serialization.
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder ForSliders()
        {
            return WithCore(true)
                   .WithParameters(true)
                   .WithCategories(ComponentCategory.Slider | ComponentCategory.MultidimensionalSlider)
                   .Include("CurrentValue", "Minimum", "Maximum", "Range", "Decimals");
        }

        /// <summary>
        /// Configures the builder for panel-specific serialization.
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder ForPanels()
        {
            return WithCore(true)
                   .WithCategories(ComponentCategory.Panel)
                   .Include("UserText", "Font", "Alignment");
        }

        /// <summary>
        /// Configures the builder for script component serialization.
        /// </summary>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder ForScripts()
        {
            return WithCore(true)
                   .WithParameters(true)
                   .WithCategories(ComponentCategory.Script)
                   .Include("Script", "MarshInputs", "MarshOutputs");
        }

        /// <summary>
        /// Applies a custom configuration action to the rule.
        /// </summary>
        /// <param name="configureAction">Action to configure the rule.</param>
        /// <returns>Builder instance for method chaining.</returns>
        public PropertyFilterBuilder Configure(Action<PropertyFilterRule> configureAction)
        {
            configureAction?.Invoke(_rule);
            return this;
        }

        /// <summary>
        /// Builds the final PropertyFilterRule.
        /// </summary>
        /// <returns>Configured PropertyFilterRule instance.</returns>
        public PropertyFilterRule Build()
        {
            return new PropertyFilterRule
            {
                IncludeCore = _rule.IncludeCore,
                IncludeParameters = _rule.IncludeParameters,
                IncludeComponents = _rule.IncludeComponents,
                IncludeCategories = _rule.IncludeCategories,
                AdditionalIncludes = new HashSet<string>(_rule.AdditionalIncludes),
                AdditionalExcludes = new HashSet<string>(_rule.AdditionalExcludes)
            };
        }

        /// <summary>
        /// Builds and creates a PropertyFilter with the configured rule.
        /// </summary>
        /// <returns>PropertyFilter instance with the configured rule.</returns>
        public PropertyFilter BuildFilter()
        {
            return PropertyFilter.CreateCustom(Build());
        }

        /// <summary>
        /// Builds and creates a PropertyManagerV2 with the configured rule.
        /// </summary>
        /// <returns>PropertyManagerV2 instance with the configured rule.</returns>
        public PropertyManagerV2 BuildManager()
        {
            return PropertyManagerV2.CreateCustom(Build());
        }
    }

    /// <summary>
    /// Extension methods for common property filter configurations.
    /// </summary>
    public static class PropertyFilterBuilderExtensions
    {
        /// <summary>
        /// Creates a filter optimized for AI processing with clean, predictable structure.
        /// </summary>
        public static PropertyFilterBuilder ForAI(this PropertyFilterBuilder builder)
        {
            return builder.WithCore(true)
                         .WithParameters(true)
                         .WithComponents(true)
                         .WithEssentialCategories()
                         .Exclude("VolatileData", "IsValid", "TypeDescription");
        }

        /// <summary>
        /// Creates a filter for debugging that includes extra information.
        /// </summary>
        public static PropertyFilterBuilder ForDebugging(this PropertyFilterBuilder builder)
        {
            return builder.Maximum()
                         .Include("InstanceDescription", "ComponentGuid", "InstanceGuid");
        }

        /// <summary>
        /// Creates a filter that excludes all runtime-only properties.
        /// </summary>
        public static PropertyFilterBuilder ExcludeRuntime(this PropertyFilterBuilder builder)
        {
            return builder.Exclude("VolatileData", "IsValid", "IsValidWhyNot",
                                 "TypeDescription", "TypeName", "Boundingbox",
                                 "ClippingBox", "ReferenceID", "IsReferencedGeometry",
                                 "IsGeometryLoaded", "QC_Type");
        }
    }
}
