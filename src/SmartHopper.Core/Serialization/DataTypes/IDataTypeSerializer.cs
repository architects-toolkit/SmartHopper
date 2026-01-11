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

using System;

namespace SmartHopper.Core.Serialization.DataTypes
{
    /// <summary>
    /// Interface for serializing and deserializing specific data types to/from string format.
    /// </summary>
    public interface IDataTypeSerializer
    {
        /// <summary>
        /// Gets the type name used in JSON serialization (e.g., "Point3d", "Color").
        /// </summary>
        string TypeName { get; }

        /// <summary>
        /// Gets the target .NET type this serializer handles.
        /// </summary>
        Type TargetType { get; }

        /// <summary>
        /// Serializes a value to a compact string format.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <returns>A string representation of the value.</returns>
        string Serialize(object value);

        /// <summary>
        /// Deserializes a string value back to the target type.
        /// </summary>
        /// <param name="value">The string value to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        object Deserialize(string value);

        /// <summary>
        /// Validates whether a string value can be deserialized.
        /// </summary>
        /// <param name="value">The string value to validate.</param>
        /// <returns>True if the value is valid, false otherwise.</returns>
        bool Validate(string value);
    }
}
