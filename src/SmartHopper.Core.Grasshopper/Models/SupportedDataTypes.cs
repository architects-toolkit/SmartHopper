/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Core.Grasshopper.Models
{
    /// <summary>
    /// Defines the Grasshopper data types and structures supported by SmartHopper
    /// </summary>
    public static class SupportedDataTypes
    {
        // Primitive types
        public const string Text = "Text";
        public const string Number = "Number";
        public const string Integer = "Integer";
        public const string Boolean = "Boolean";

        // Data structures
        public const string Item = "Item";
        public const string List = "List";
        public const string Tree = "Tree";

        // Special types
        public const string Generic = "Generic";
        public const string Geometry = "Geometry";
        public const string Color = "Colour";

        /// <summary>
        /// Gets all supported primitive data types
        /// </summary>
        public static IReadOnlyList<string> PrimitiveTypes { get; } = new[]
        {
            Text,
            Number,
            Integer,
            Boolean
        };

        /// <summary>
        /// Gets all supported data structures
        /// </summary>
        public static IReadOnlyList<string> DataStructures { get; } = new[]
        {
            Item,
            List,
            Tree
        };

        /// <summary>
        /// Gets all special type categories
        /// </summary>
        public static IReadOnlyList<string> SpecialTypes { get; } = new[]
        {
            Generic,
            Geometry,
            Color
        };

        /// <summary>
        /// Determines if the specified type is a supported primitive type
        /// </summary>
        public static bool IsPrimitiveType(string type) =>
            PrimitiveTypes.Contains(type);

        /// <summary>
        /// Determines if the specified type is a supported data structure
        /// </summary>
        public static bool IsDataStructure(string type) =>
            DataStructures.Contains(type);

        /// <summary>
        /// Determines if the specified type is numeric (Number or Integer)
        /// </summary>
        public static bool IsNumeric(string type) =>
            type == Number || type == Integer;

        /// <summary>
        /// Determines if the specified type is a collection (List or Tree)
        /// </summary>
        public static bool IsCollection(string type) =>
            type == List || type == Tree;

        /// <summary>
        /// Determines if the specified type is valid (supported primitive, data structure or special type)
        /// </summary>
        public static bool IsValidType(string type) =>
            IsPrimitiveType(type) || IsDataStructure(type) || SpecialTypes.Contains(type);
    }
}
