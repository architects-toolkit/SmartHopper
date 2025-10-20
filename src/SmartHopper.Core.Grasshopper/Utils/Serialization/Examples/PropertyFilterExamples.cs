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
using Grasshopper.Kernel.Special;
using SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization.Examples
{
    /// <summary>
    /// Examples demonstrating how to use the new property filtering system.
    /// These examples show various common scenarios and configurations.
    /// </summary>
    public static class PropertyFilterExamples
    {
        /// <summary>
        /// Example 1: Basic usage with predefined contexts
        /// </summary>
        public static void BasicUsageExample()
        {
            // Create managers for different contexts
            var aiManager = PropertyManagerFactory.CreateForAI();
            var fullManager = PropertyManagerFactory.CreateForFullSerialization();
            var compactManager = PropertyManagerFactory.CreateForCompactSerialization();

            // Use with any Grasshopper object
            var slider = new GH_NumberSlider();
            
            var aiProperties = aiManager.ExtractProperties(slider);
            var fullProperties = fullManager.ExtractProperties(slider);
            var compactProperties = compactManager.ExtractProperties(slider);

            Console.WriteLine($"AI context extracted {aiProperties.Count} properties");
            Console.WriteLine($"Full context extracted {fullProperties.Count} properties");
            Console.WriteLine($"Compact context extracted {compactProperties.Count} properties");
        }

        /// <summary>
        /// Example 2: Custom filtering with builder pattern
        /// </summary>
        public static void CustomFilteringExample()
        {
            // Build a custom filter for sliders only
            var sliderManager = PropertyFilterBuilder
                .Create()
                .ForSliders()
                .Include("DisplayFormat", "TickCount")
                .Exclude("Minimum", "Maximum")
                .BuildManager();

            // Build a filter for panels with custom font handling
            var panelManager = PropertyFilterBuilder
                .FromContext(SerializationContext.AIOptimized)
                .ForPanels()
                .Include("BackgroundColor", "BorderColor")
                .ExcludeDataType(true)
                .BuildManager();

            // Build a minimal filter for parameters only
            var parameterManager = PropertyFilterBuilder
                .Create()
                .Minimal()
                .WithParameters(true)
                .Include("Expression", "DataMapping")
                .BuildManager();
        }

        /// <summary>
        /// Example 3: Component category-based filtering
        /// </summary>
        public static void CategoryBasedFilteringExample()
        {
            // Include only UI components
            var uiManager = PropertyManagerFactory.CreateWithCategories(
                ComponentCategory.UI, 
                excludeDataType: true);

            // Include essential + data components
            var dataManager = PropertyManagerFactory.CreateWithCategories(
                ComponentCategory.Essential | ComponentCategory.Data);

            // Include everything except advanced components
            var basicManager = PropertyFilterBuilder
                .Create()
                .Maximum()
                .RemoveCategories(ComponentCategory.Advanced)
                .BuildManager();
        }

        /// <summary>
        /// Example 4: Context-specific configurations
        /// </summary>
        public static void ContextSpecificExample()
        {
            // AI-optimized with additional debugging info
            var aiDebugManager = PropertyFilterBuilder
                .FromContext(SerializationContext.AIOptimized)
                .ForDebugging()
                .Include("LastModified", "CreatedBy")
                .BuildManager();

            // Compact serialization but keep expressions
            var compactWithExpressionsManager = PropertyFilterBuilder
                .FromContext(SerializationContext.CompactSerialization)
                .Include("Expression", "ExpressionNormal", "ExpressionPressed")
                .BuildManager();

            // Full serialization but exclude runtime data
            var fullNoRuntimeManager = PropertyFilterBuilder
                .FromContext(SerializationContext.FullSerialization)
                .ExcludeRuntime()
                .BuildManager();
        }

        /// <summary>
        /// Example 5: Dynamic filtering based on object type
        /// </summary>
        public static PropertyManagerV2 CreateDynamicManager(object grasshopperObject)
        {
            return grasshopperObject switch
            {
                GH_NumberSlider => PropertyFilterBuilder.Create().ForSliders().BuildManager(),
                GH_Panel => PropertyFilterBuilder.Create().ForPanels().BuildManager(),
                GH_Scribble => PropertyFilterBuilder.Create()
                    .WithCore(true)
                    .WithCategories(ComponentCategory.Scribble)
                    .Include("Text", "Font", "Corners")
                    .BuildManager(),
                _ => PropertyManagerFactory.CreateForAI()
            };
        }

        /// <summary>
        /// Example 6: Conditional property inclusion
        /// </summary>
        public static void ConditionalFilteringExample()
        {
            // Create a filter that includes different properties based on conditions
            var conditionalRule = PropertyFilterBuilder
                .Create()
                .WithCore(true)
                .WithParameters(true)
                .Configure(rule =>
                {
                    // Add conditional logic
                    if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
                    {
                        rule.AdditionalIncludes.Add("DebugInfo");
                    }
                    
                    if (Environment.GetEnvironmentVariable("SMARTHOPPER_DEBUG") == "1")
                    {
                        rule.IncludeCategories |= ComponentCategory.Advanced;
                    }
                })
                .Build();

            var conditionalManager = PropertyManagerV2.CreateCustom(conditionalRule);
        }

        /// <summary>
        /// Example 7: Property extraction analysis
        /// </summary>
        public static void AnalysisExample()
        {
            var manager = PropertyManagerFactory.CreateForAI();
            var slider = new GH_NumberSlider();

            // Get extraction summary for analysis
            var summary = manager.CreateExtractionSummary(slider);
            
            Console.WriteLine($"Object Type: {summary.ObjectType}");
            Console.WriteLine($"Total Properties: {summary.TotalProperties}");
            Console.WriteLine($"Allowed Properties: {string.Join(", ", summary.AllowedProperties)}");
            Console.WriteLine($"Excluded Properties: {string.Join(", ", summary.ExcludedProperties)}");

            // Check specific properties
            var shouldIncludeCurrentValue = manager.ShouldIncludeProperty("CurrentValue", slider);
            var shouldIncludeVolatileData = manager.ShouldIncludeProperty("VolatileData", slider);
            
            Console.WriteLine($"Should include CurrentValue: {shouldIncludeCurrentValue}");
            Console.WriteLine($"Should include VolatileData: {shouldIncludeVolatileData}");
        }

        /// <summary>
        /// Example 8: Migration from old PropertyManager
        /// </summary>
        public static void MigrationExample()
        {
            // Old way (hardcoded, difficult to maintain)
            // var oldManager = new PropertyManager();
            // var isAllowed = oldManager.IsPropertyInWhitelist("CurrentValue");

            // New way (flexible, maintainable)
            var newManager = PropertyManagerFactory.CreateForAI();
            var slider = new GH_NumberSlider();
            var isAllowed = newManager.ShouldIncludeProperty("CurrentValue", slider);

            // Extract properties the new way
            var properties = newManager.ExtractProperties(slider);
            
            // Apply properties the new way
            var targetSlider = new GH_NumberSlider();
            var results = newManager.ApplyProperties(targetSlider, properties);
            
            foreach (var result in results)
            {
                Console.WriteLine($"Property {result.Key}: {(result.Value ? "Applied" : "Failed")}");
            }
        }

        /// <summary>
        /// Example 9: Performance comparison
        /// </summary>
        public static void PerformanceExample()
        {
            var objects = new List<object>
            {
                new GH_NumberSlider(),
                new GH_Panel(),
                new GH_ValueList()
            };

            // Measure extraction time with different contexts
            var contexts = new[]
            {
                SerializationContext.CompactSerialization,
                SerializationContext.AIOptimized,
                SerializationContext.FullSerialization
            };

            foreach (var context in contexts)
            {
                var manager = new PropertyManagerV2(context);
                var start = DateTime.Now;

                foreach (var obj in objects)
                {
                    var properties = manager.ExtractProperties(obj);
                }

                var elapsed = DateTime.Now - start;
                Console.WriteLine($"Context {context}: {elapsed.TotalMilliseconds}ms");
            }
        }

        /// <summary>
        /// Example 10: Advanced customization
        /// </summary>
        public static void AdvancedCustomizationExample()
        {
            // Create a highly customized filter
            var advancedManager = PropertyFilterBuilder
                .Create()
                .WithCore(true)
                .WithParameters(true)
                .WithComponents(false)
                .WithCategories(ComponentCategory.Essential | ComponentCategory.UI)
                .Include("CustomProperty1", "CustomProperty2")
                .Exclude("LegacyProperty1", "LegacyProperty2")
                .ExcludeDataType(true)
                .Configure(rule =>
                {
                    // Advanced configuration
                    rule.AdditionalIncludes.Add("RuntimeCalculatedProperty");
                    
                    // Conditional exclusions
                    if (Environment.MachineName.StartsWith("DEV"))
                    {
                        rule.AdditionalIncludes.Add("DeveloperOnlyProperty");
                    }
                })
                .BuildManager();

            // Use the advanced manager
            var panel = new GH_Panel();
            var properties = advancedManager.ExtractProperties(panel);
            
            Console.WriteLine($"Advanced extraction yielded {properties.Count} properties");
        }
    }
}
