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
            
            // Redundant metadata
            "humanReadable",
            
            // Internal framework properties
            "Properties" // Legacy properties dictionary
        };

        /// <summary>
        /// Properties that are essential for all Grasshopper objects.
        /// These form the core serialization set.
        /// </summary>
        public static readonly HashSet<string> CoreProperties = new()
        {
            "NickName",
            "Locked",
            "PersistentData"
        };

        /// <summary>
        /// Properties specific to parameters (IGH_Param implementations).
        /// </summary>
        public static readonly HashSet<string> ParameterProperties = new()
        {
            "DataMapping",
            "Simplify", 
            "Reverse",
            "Expression",
            "Invert"
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
                "UserText",
                "Font",
                "Alignment"
            },

            [ComponentCategory.Scribble] = new()
            {
                "Text",
                "Font", 
                "Corners"
            },

            [ComponentCategory.Slider] = new()
            {
                "CurrentValue",
                "Minimum",
                "Maximum", 
                "Range",
                "Decimals",
                "Limit",
                "DisplayFormat"
            },

            [ComponentCategory.MultidimensionalSlider] = new()
            {
                "SliderMode",
                "XInterval",
                "YInterval", 
                "ZInterval",
                "X",
                "Y",
                "Z"
            },

            [ComponentCategory.ValueList] = new()
            {
                "ListMode",
                "ListItems"
            },

            [ComponentCategory.Button] = new()
            {
                "ExpressionNormal",
                "ExpressionPressed"
            },

            [ComponentCategory.Script] = new()
            {
                "Script",
                "MarshInputs",
                "MarshOutputs"
            },

            [ComponentCategory.GeometryPipeline] = new()
            {
                "LayerFilter",
                "NameFilter",
                "TypeFilter", 
                "IncludeLocked",
                "IncludeHidden",
                "GroupByLayer",
                "GroupByType"
            },

            [ComponentCategory.GraphMapper] = new()
            {
                "GraphType"
            },

            [ComponentCategory.PathMapper] = new()
            {
                "Lexers"
            },

            [ComponentCategory.ColorWheel] = new()
            {
                "State"
            },

            [ComponentCategory.DataRecorder] = new()
            {
                "DataLimit",
                "RecordData"
            },

            [ComponentCategory.ItemPicker] = new()
            {
                "TreePath",
                "TreeIndex"
            }
        };

        /// <summary>
        /// Context-specific filtering rules.
        /// Different serialization contexts may need different property sets.
        /// </summary>
        public static readonly Dictionary<SerializationContext, PropertyFilterRule> ContextRules = new()
        {
            [SerializationContext.FullSerialization] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = true,
                IncludeCategories = ComponentCategory.All,
                ExcludeDataType = false // Include for backward compatibility
            },

            [SerializationContext.CompactSerialization] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = false,
                IncludeCategories = ComponentCategory.Essential,
                ExcludeDataType = true
            },

            [SerializationContext.AIOptimized] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = true,
                IncludeCategories = ComponentCategory.Essential | ComponentCategory.UI,
                ExcludeDataType = true
            },

            [SerializationContext.ParametersOnly] = new()
            {
                IncludeCore = true,
                IncludeParameters = true,
                IncludeComponents = false,
                IncludeCategories = ComponentCategory.None,
                ExcludeDataType = true
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
        Script = 1 << 6,
        GeometryPipeline = 1 << 7,
        GraphMapper = 1 << 8,
        PathMapper = 1 << 9,
        ColorWheel = 1 << 10,
        DataRecorder = 1 << 11,
        ItemPicker = 1 << 12,

        // Convenience combinations
        Essential = Panel | Scribble | Slider | ValueList | Script,
        UI = Panel | Scribble | Button | ColorWheel,
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
        /// Include all properties for complete fidelity.
        /// </summary>
        FullSerialization,

        /// <summary>
        /// Include only essential properties for smaller output.
        /// </summary>
        CompactSerialization,

        /// <summary>
        /// Optimized for AI processing - clean, predictable structure.
        /// </summary>
        AIOptimized,

        /// <summary>
        /// Only parameter-related properties.
        /// </summary>
        ParametersOnly
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
        public bool ExcludeDataType { get; set; } = true;
        public HashSet<string> AdditionalIncludes { get; set; } = new();
        public HashSet<string> AdditionalExcludes { get; set; } = new();
    }
}
