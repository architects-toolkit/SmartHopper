/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Drawing;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Configuration options for GhJSON deserialization (JSON to components).
    /// Controls how components are created and configured from JSON data.
    /// </summary>
    public class DeserializationOptions
    {
        /// <summary>
        /// Whether to apply component properties from the JSON.
        /// </summary>
        public bool ApplyProperties { get; set; } = true;

        /// <summary>
        /// Whether to apply parameter settings (nicknames, access modes, etc.).
        /// </summary>
        public bool ApplyParameterSettings { get; set; } = true;

        /// <summary>
        /// Whether to inject type hints into script component code.
        /// </summary>
        public bool InjectScriptTypeHints { get; set; } = true;

        /// <summary>
        /// Whether to apply component state (enabled/locked/hidden).
        /// </summary>
        public bool ApplyComponentState { get; set; } = true;

        /// <summary>
        /// Whether to apply parameter expressions.
        /// </summary>
        public bool ApplyParameterExpressions { get; set; } = true;

        /// <summary>
        /// Whether to recreate wire connections between components.
        /// </summary>
        public bool CreateConnections { get; set; } = true;

        /// <summary>
        /// Whether to recreate groups.
        /// </summary>
        public bool CreateGroups { get; set; } = true;

        /// <summary>
        /// Whether to place components on the canvas.
        /// If false, components are created but not positioned.
        /// </summary>
        public bool PlaceOnCanvas { get; set; } = true;

        /// <summary>
        /// Starting position for placing components on canvas.
        /// If null, uses default positioning logic.
        /// </summary>
        public PointF? StartPosition { get; set; } = null;

        /// <summary>
        /// Spacing between components when positioning.
        /// </summary>
        public int ComponentSpacing { get; set; } = 100;

        /// <summary>
        /// Whether to validate component types before instantiation.
        /// </summary>
        public bool ValidateComponentTypes { get; set; } = true;

        /// <summary>
        /// Whether to replace integer IDs with proper GUIDs during deserialization.
        /// </summary>
        public bool ReplaceIntegerIds { get; set; } = true;

        /// <summary>
        /// Creates default options for standard deserialization.
        /// </summary>
        public static DeserializationOptions Standard => new DeserializationOptions
        {
            ApplyProperties = true,
            ApplyParameterSettings = true,
            InjectScriptTypeHints = true,
            ApplyComponentState = true,
            ApplyParameterExpressions = true,
            CreateConnections = true,
            CreateGroups = true,
            PlaceOnCanvas = true,
            StartPosition = null,
            ComponentSpacing = 100,
            ValidateComponentTypes = true,
            ReplaceIntegerIds = true
        };

        /// <summary>
        /// Creates options for component creation only (no placement).
        /// </summary>
        public static DeserializationOptions ComponentsOnly => new DeserializationOptions
        {
            ApplyProperties = true,
            ApplyParameterSettings = true,
            InjectScriptTypeHints = true,
            ApplyComponentState = true,
            ApplyParameterExpressions = true,
            CreateConnections = false,
            CreateGroups = false,
            PlaceOnCanvas = false,
            ValidateComponentTypes = true,
            ReplaceIntegerIds = true
        };

        /// <summary>
        /// Creates options for minimal deserialization (structure only).
        /// </summary>
        public static DeserializationOptions Minimal => new DeserializationOptions
        {
            ApplyProperties = false,
            ApplyParameterSettings = false,
            InjectScriptTypeHints = false,
            ApplyComponentState = false,
            ApplyParameterExpressions = false,
            CreateConnections = true,
            CreateGroups = false,
            PlaceOnCanvas = true,
            ValidateComponentTypes = true,
            ReplaceIntegerIds = true
        };
    }
}
