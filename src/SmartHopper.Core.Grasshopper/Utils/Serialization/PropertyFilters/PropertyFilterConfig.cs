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
    /// Defines property filtering rules for different object types and contexts.
    /// This provides a centralized, maintainable way to specify which properties
    /// should be included or excluded during serialization.
    /// </summary>
    public static class PropertyFilterConfig
    {
        /// <summary>
        /// Properties that should NEVER be serialized regardless of context.
        /// These are typically runtime-only or redundant properties.
        /// </summary>
        public static readonly HashSet<string> GlobalBlacklist = new()
        {
            // Runtime-only properties
            "VolatileData",
            "IsValid",
            "IsValidWhyNot",
            "TypeDescription",
            "TypeName",
            "Boundingbox",
            "ClippingBox",
            "ReferenceID",
            "IsReferencedGeometry",
            "IsGeometryLoaded",
            "QC_Type",

            // GH_ValueListItem UI/runtime properties (handled by specialized handler)
            "BoxName",
            "BoxLeft",
            "BoxRight",
            "Value",
            "IsVisible",

            // Redundant metadata
            "humanReadable",

            // Internal framework properties
            "Properties", // Legacy properties dictionary
        };

        /// <summary>
        /// Properties that are essential for all Grasshopper objects.
        /// These form the core serialization set.
        /// </summary>
        public static readonly HashSet<string> CoreProperties = new()
        {
            "NickName",
            "Locked",
            "PersistentData",  // Data for parameters with no sources (restored on deserialization)
            // "VolatileData",    // Data for parameters with sources (AI context only, not restored)
        };

        /// <summary>
        /// Properties specific to parameters (IGH_Param implementations).
        /// Note: IsPrincipal is NOT included here because it's handled specifically
        /// in inputSettings/outputSettings additionalSettings, not as a top-level property.
        /// </summary>
        public static readonly HashSet<string> ParameterProperties = new()
        {
            "DataMapping",
            "Simplify",
            "Reverse",
            "Expression",
            "Invert",
            "Locked"
        };

        /// <summary>
        /// Properties specific to components (IGH_Component implementations).
        /// </summary>
        public static readonly HashSet<string> ComponentProperties = new()
        {
            "Hidden",
            "DisplayName"
        };

        /// <summary>
        /// UI-specific properties for special component types.
        /// Organized by component category for easy maintenance.
        /// </summary>
        public static readonly Dictionary<ComponentCategory, HashSet<string>> CategoryProperties = new()
        {
            [ComponentCategory.Panel] = new()
            {
                // UserText is consolidated into componentState.value; do not serialize as a separate property
                "Font",
                "Alignment",
                "Multiline",
                "DrawIndices",
                "DrawPaths",
                "SpecialCodes",
            },

            [ComponentCategory.Scribble] = new()
            {
                "Text",
                "Font",
                "Corners",
            },

            [ComponentCategory.Slider] = new()
            {
                "CurrentValue",
                "Minimum",
                "Maximum",
                "Range",
                "Decimals",
                "Rounding",
                "Limit",
                "DisplayFormat",
            },

            [ComponentCategory.MultidimensionalSlider] = new()
            {
                "SliderMode",
                "XInterval",
                "YInterval",
                "ZInterval",
                "X",
                "Y",
                "Z",
            },

            [ComponentCategory.ValueList] = new()
            {
                "ListMode",
                "ListItems",
                "SelectedIndices",
            },

            [ComponentCategory.Button] = new()
            {
                "ExpressionNormal",
                "ExpressionPressed",
            },

            [ComponentCategory.BooleanToggle] = new()
            {
                // Boolean toggle uses UniversalValue for state
            },

            [ComponentCategory.ColourSwatch] = new()
            {
                // Colour swatch uses UniversalValue for color
            },

            [ComponentCategory.Script] = new()
            {
                "Script",
                "MarshInputs",
                "MarshOutputs",
                "VariableName",
            },

            [ComponentCategory.GeometryPipeline] = new()
            {
                "LayerFilter",
                "NameFilter",
                "TypeFilter",
                "IncludeLocked",
                "IncludeHidden",
                "GroupByLayer",
                "GroupByType",
            },

            [ComponentCategory.GraphMapper] = new()
            {
                "GraphType",
            },

            [ComponentCategory.PathMapper] = new()
            {
                "Lexers",
            },

            [ComponentCategory.ColorWheel] = new()
            {
                "State",
            },

            [ComponentCategory.DataRecorder] = new()
            {
                "DataLimit",
                "RecordData",
            },

            [ComponentCategory.ItemPicker] = new()
            {
                "TreePath",
                "TreeIndex",
            }
        };

        /// <summary>
        /// Context-specific filtering rules.
        /// Different serialization contexts may need different property sets.
        /// </summary>
        public static readonly Dictionary<SerializationContext, PropertyFilterRule> ContextRules = new()
        {
            [SerializationContext.Standard] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = true,
                IncludeCategories = ComponentCategory.Essential | ComponentCategory.UI,
            },

            [SerializationContext.Optimized] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = true,
                IncludeCategories = ComponentCategory.Essential | ComponentCategory.UI,
                AdditionalExcludes = new()
                {
                    "PersistentData",
                },
            },

            [SerializationContext.Lite] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = false,  // No componentState in Lite format
                IncludeCategories = ComponentCategory.Essential,
                AdditionalExcludes = new() {
                    // General
                    "ComponentGuid", "InstanceGuid", "Selected", "DisplayName",

                    // Panel Visualization
                    "Alignment", "Font", "SpecialCodes", "DrawIndices", "DrawPaths",

                    // Persistent Data
                    "PersistentData",
                }
            }
        };
    }

    /// <summary>
    /// Defines component categories for property filtering.
    /// </summary>
    [Flags]
    public enum ComponentCategory
    {
        None = 0,
        Panel = 1 << 0,
        Scribble = 1 << 1,
        Slider = 1 << 2,
        MultidimensionalSlider = 1 << 3,
        ValueList = 1 << 4,
        Button = 1 << 5,
        BooleanToggle = 1 << 6,
        ColourSwatch = 1 << 7,
        Script = 1 << 8,
        GeometryPipeline = 1 << 9,
        GraphMapper = 1 << 10,
        PathMapper = 1 << 11,
        ColorWheel = 1 << 12,
        DataRecorder = 1 << 13,
        ItemPicker = 1 << 14,

        // Convenience combinations
        Essential = Panel | Scribble | Slider | ValueList | Script,
        UI = Panel | Scribble | Button | BooleanToggle | ColourSwatch | ColorWheel,
        Data = ValueList | DataRecorder | ItemPicker,
        Advanced = GeometryPipeline | GraphMapper | PathMapper,
        All = ~None
    }

    /// <summary>
    /// Defines different serialization contexts that may require different property sets.
    /// </summary>
    public enum SerializationContext
    {
        /// <summary>
        /// Standard format - balanced, clean structure for AI processing.
        /// Includes core properties, parameters, and essential UI components.
        /// This is the default format used throughout SmartHopper.
        /// </summary>
        Standard,

        /// <summary>
        /// Optimized format - same as Standard, but suppresses bulky data fields.
        /// Excludes PersistentData to reduce token usage, while
        /// keeping the same structural schema as Standard.
        /// </summary>
        Optimized,

        /// <summary>
        /// Lite format - compressed variant optimized for minimal token usage.
        /// Excludes GUIDs, UI state, and component-specific properties.
        /// Planned for Phase 2.1 implementation.
        /// </summary>
        Lite
    }

    /// <summary>
    /// Defines filtering rules for a specific serialization context.
    /// </summary>
    public class PropertyFilterRule
    {
        public bool IncludeCore { get; set; } = true;
        public bool IncludeParameters { get; set; } = true;
        public bool IncludeComponents { get; set; } = true;
        public ComponentCategory IncludeCategories { get; set; } = ComponentCategory.All;
        public HashSet<string> AdditionalIncludes { get; set; } = new();
        public HashSet<string> AdditionalExcludes { get; set; } = new();
    }
}
