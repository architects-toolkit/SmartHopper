/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace SmartHopper.Core.JSON
{
    /// <summary>
    /// Represents the properties and metadata of a Grasshopper component.
    /// </summary>
    public class ComponentProperties
    {
        [JsonProperty("name")]
        [JsonRequired]
        public string Name { get; set; }

        [JsonProperty("type")]
        [JsonRequired]
        public string Type { get; set; }

        [JsonProperty("objectType")]
        [JsonRequired]
        public string ObjectType { get; set; }

        [JsonProperty("componentGuid")]
        [JsonRequired]
        public Guid ComponentGuid { get; set; }

        [JsonProperty("instanceGuid")]
        [JsonRequired]
        public Guid InstanceGuid { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string, ComponentProperty> Properties { get; set; } = new Dictionary<string, ComponentProperty>();

        [JsonProperty("selected")]
        public bool Selected { get; set; }

        [JsonProperty("pivot")]
        public PointF Pivot { get; set; }

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        [JsonProperty("errors")]
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Checks if the component has any validation errors or warnings.
        /// </summary>
        public bool HasIssues => Warnings.Any() || Errors.Any();

        /// <summary>
        /// Gets a property value by its key, with optional type conversion.
        /// </summary>
        public T GetPropertyValue<T>(string key, T defaultValue = default)
        {
            if (Properties.TryGetValue(key, out var property) && property.Value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(property.Value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Sets a property value with type information and optional human-readable format.
        /// </summary>
        public void SetProperty(string key, object value, string humanReadable = null)
        {
            Properties[key] = new ComponentProperty
            {
                Value = value,
                Type = value?.GetType().Name ?? "null",
                HumanReadable = humanReadable ?? value?.ToString()
            };
        }
    }

    /// <summary>
    /// Represents a property of a Grasshopper component with type information and human-readable format.
    /// </summary>
    public class ComponentProperty
    {
        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("type")]
        [JsonRequired]
        public string Type { get; set; }

        [JsonProperty("humanReadable")]
        public string HumanReadable { get; set; }

        /// <summary>
        /// Creates a new ComponentProperty with the specified value and optional human-readable format.
        /// </summary>
        //public static ComponentProperty Create<T>(T value, string humanReadable = null)
        //{
        //    return new ComponentProperty
        //    {
        //        Value = value,
        //        Type = typeof(T).Name,
        //        HumanReadable = humanReadable ?? value?.ToString()
        //    };
        //}
    }

    /// <summary>
    /// Represents a connection endpoint in a Grasshopper document.
    /// </summary>
    public struct Connection
    {
        [JsonProperty("componentId")]
        [JsonRequired]
        public Guid ComponentId { get; set; }

        [JsonProperty("paramName")]
        [JsonRequired]
        public string ParamName { get; set; }

        /// <summary>
        /// Checks if the connection has valid component ID and parameter name.
        /// </summary>
        public bool IsValid()
        {
            return ComponentId != Guid.Empty && !string.IsNullOrEmpty(ParamName);
        }

        /// <summary>
        /// Creates a new Connection from a Grasshopper parameter.
        /// </summary>
        public static Connection FromParameter(IGH_Param param)
        {
            return new Connection
            {
                ComponentId = param.InstanceGuid,
                ParamName = param.Name
            };
        }
    }

    /// <summary>
    /// Represents a connection between two components in a Grasshopper document.
    /// </summary>
    public struct ConnectionPairing
    {
        [JsonProperty("to")]
        public Connection To { get; set; }

        [JsonProperty("from")]
        public Connection From { get; set; }

        /// <summary>
        /// Checks if both endpoints of the connection are valid.
        /// </summary>
        public bool IsValid()
        {
            return To.IsValid() && From.IsValid();
        }

        /// <summary>
        /// Creates a new ConnectionPairing from source and target parameters.
        /// </summary>
        public static ConnectionPairing Create(IGH_Param source, IGH_Param target)
        {
            return new ConnectionPairing
            {
                From = Connection.FromParameter(source),
                To = Connection.FromParameter(target)
            };
        }
    }

    /// <summary>
    /// Represents a complete Grasshopper document with components and their connections.
    /// </summary>
    public class GrasshopperDocument
    {
        [JsonProperty("components")]
        public List<ComponentProperties> Components { get; set; } = new List<ComponentProperties>();

        [JsonProperty("connections")]
        public List<ConnectionPairing> Connections { get; set; } = new List<ConnectionPairing>();

        /// <summary>
        /// Gets all components with validation issues (errors or warnings).
        /// </summary>
        public IEnumerable<ComponentProperties> GetComponentsWithIssues()
        {
            return Components.Where(c => c.HasIssues);
        }

        /// <summary>
        /// Gets all connections for a specific component.
        /// </summary>
        public IEnumerable<ConnectionPairing> GetComponentConnections(Guid componentId)
        {
            return Connections.Where(c =>
                c.From.ComponentId == componentId ||
                c.To.ComponentId == componentId);
        }

        /// <summary>
        /// Gets all input connections for a specific component.
        /// </summary>
        public IEnumerable<ConnectionPairing> GetComponentInputs(Guid componentId)
        {
            return Connections.Where(c => c.To.ComponentId == componentId);
        }

        /// <summary>
        /// Gets all output connections for a specific component.
        /// </summary>
        public IEnumerable<ConnectionPairing> GetComponentOutputs(Guid componentId)
        {
            return Connections.Where(c => c.From.ComponentId == componentId);
        }
    }
}
