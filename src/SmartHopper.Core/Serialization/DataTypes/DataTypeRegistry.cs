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
using System.Diagnostics;
using SmartHopper.Core.Serialization.DataTypes.Serializers;

namespace SmartHopper.Core.Serialization.DataTypes
{
    /// <summary>
    /// Thread-safe singleton registry for data type serializers.
    /// </summary>
    public sealed class DataTypeRegistry
    {
        private static readonly Lazy<DataTypeRegistry> instance = new Lazy<DataTypeRegistry>(() => new DataTypeRegistry());
        private readonly Dictionary<string, IDataTypeSerializer> serializersByName;
        private readonly Dictionary<Type, IDataTypeSerializer> serializersByType;
        private readonly object lockObject = new object();

        /// <summary>
        /// Gets the singleton instance of the registry.
        /// </summary>
        public static DataTypeRegistry Instance => instance.Value;

        private DataTypeRegistry()
        {
            this.serializersByName = new Dictionary<string, IDataTypeSerializer>(StringComparer.OrdinalIgnoreCase);
            this.serializersByType = new Dictionary<Type, IDataTypeSerializer>();

            // Register all built-in serializers
            this.RegisterBuiltInSerializers();
        }

        /// <summary>
        /// Registers a serializer for a specific data type.
        /// </summary>
        /// <param name="serializer">The serializer to register.</param>
        public void RegisterSerializer(IDataTypeSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            lock (this.lockObject)
            {
                this.serializersByName[serializer.TypeName] = serializer;
                this.serializersByType[serializer.TargetType] = serializer;
                Debug.WriteLine($"[DataTypeRegistry] Registered serializer for type: {serializer.TypeName}");
            }
        }

        /// <summary>
        /// Gets a serializer by type name.
        /// </summary>
        /// <param name="typeName">The type name (e.g., "Point3d", "Color").</param>
        /// <returns>The serializer, or null if not found.</returns>
        public IDataTypeSerializer GetSerializer(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            lock (this.lockObject)
            {
                this.serializersByName.TryGetValue(typeName, out IDataTypeSerializer serializer);
                return serializer;
            }
        }

        /// <summary>
        /// Gets a serializer by .NET type.
        /// </summary>
        /// <param name="type">The .NET type.</param>
        /// <returns>The serializer, or null if not found.</returns>
        public IDataTypeSerializer GetSerializer(Type type)
        {
            if (type == null)
            {
                return null;
            }

            lock (this.lockObject)
            {
                this.serializersByType.TryGetValue(type, out IDataTypeSerializer serializer);
                return serializer;
            }
        }

        /// <summary>
        /// Checks if a serializer is registered for the given type name.
        /// </summary>
        /// <param name="typeName">The type name to check.</param>
        /// <returns>True if a serializer is registered, false otherwise.</returns>
        public bool IsRegistered(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            lock (this.lockObject)
            {
                return this.serializersByName.ContainsKey(typeName);
            }
        }

        /// <summary>
        /// Checks if a serializer is registered for the given type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if a serializer is registered, false otherwise.</returns>
        public bool IsRegistered(Type type)
        {
            if (type == null)
            {
                return false;
            }

            lock (this.lockObject)
            {
                return this.serializersByType.ContainsKey(type);
            }
        }

        private void RegisterBuiltInSerializers()
        {
            // Register all built-in serializers
            this.RegisterSerializer(new ColorSerializer());
            this.RegisterSerializer(new PointSerializer());
            this.RegisterSerializer(new VectorSerializer());
            this.RegisterSerializer(new LineSerializer());
            this.RegisterSerializer(new PlaneSerializer());
            this.RegisterSerializer(new CircleSerializer());
            this.RegisterSerializer(new ArcSerializer());
            this.RegisterSerializer(new BoundingBoxSerializer());
            this.RegisterSerializer(new DomainSerializer());

            Debug.WriteLine($"[DataTypeRegistry] Registered {this.serializersByName.Count} built-in serializers");
        }
    }
}
