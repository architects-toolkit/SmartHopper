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
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    /// <summary>
    /// Utilities for generating Grasshopper component specifications.
    /// </summary>
    public static class ComponentSpecBuilder
    {
        /// <summary>
        /// Generates a GhJSON component specification from a component name and optional parameters.
        /// </summary>
        /// <param name="componentName">Name or nickname of the component to create.</param>
        /// <param name="parameters">Optional dictionary of parameter name/value pairs.</param>
        /// <param name="position">Optional position for the component. If null, automatic layout will be used.</param>
        /// <param name="instanceGuid">Optional instance GUID. If null, a new GUID will be generated.</param>
        /// <returns>JObject representing the component in GhJSON format, or null if component not found.</returns>
        public static JObject GenerateComponentSpec(
            string componentName,
            Dictionary<string, object> parameters = null,
            PointF? position = null,
            Guid? instanceGuid = null)
        {
            if (string.IsNullOrEmpty(componentName))
            {
                Debug.WriteLine("[ComponentSpecBuilder] Component name is required.");
                return null;
            }

            // Find the component proxy
            var proxy = ObjectFactory.FindProxy(componentName);
            if (proxy == null)
            {
                Debug.WriteLine($"[ComponentSpecBuilder] Component not found: {componentName}");
                return null;
            }

            // Create a temporary instance to get metadata
            var instance = ObjectFactory.CreateInstance(proxy);
            var guid = instanceGuid ?? Guid.NewGuid();

            var ghComponent = new JObject
            {
                ["guid"] = proxy.Guid.ToString(),
                ["name"] = proxy.Desc.Name,
                ["nickname"] = proxy.Desc.NickName,
                ["instanceGuid"] = guid.ToString()
            };

            // Add position if specified
            if (position.HasValue)
            {
                ghComponent["pivot"] = new JObject
                {
                    ["x"] = position.Value.X,
                    ["y"] = position.Value.Y
                };
            }

            // Add parameter values if specified
            if (parameters != null && parameters.Count > 0)
            {
                var paramValues = new JArray();
                foreach (var kvp in parameters)
                {
                    paramValues.Add(new JObject
                    {
                        ["name"] = kvp.Key,
                        ["value"] = JToken.FromObject(kvp.Value)
                    });
                }

                ghComponent["parameterValues"] = paramValues;
            }

            // Add inputs/outputs metadata
            if (instance is IGH_Component comp)
            {
                var inputs = new JArray();
                foreach (var param in comp.Params.Input)
                {
                    inputs.Add(new JObject
                    {
                        ["name"] = param.Name,
                        ["nickname"] = param.NickName,
                        ["description"] = param.Description
                    });
                }

                var outputs = new JArray();
                foreach (var param in comp.Params.Output)
                {
                    outputs.Add(new JObject
                    {
                        ["name"] = param.Name,
                        ["nickname"] = param.NickName,
                        ["description"] = param.Description
                    });
                }

                if (inputs.Count > 0) ghComponent["inputs"] = inputs;
                if (outputs.Count > 0) ghComponent["outputs"] = outputs;
            }

            Debug.WriteLine($"[ComponentSpecBuilder] Generated component spec: {componentName} ({guid})");
            return ghComponent;
        }

        /// <summary>
        /// Generates a complete GhJSON document from multiple component specifications.
        /// </summary>
        /// <param name="componentSpecs">List of component specifications.</param>
        /// <returns>Complete GhJSON document as JObject.</returns>
        public static JObject GenerateGhJsonDocument(List<JObject> componentSpecs)
        {
            if (componentSpecs == null || componentSpecs.Count == 0)
            {
                Debug.WriteLine("[ComponentSpecBuilder] No component specifications provided.");
                return null;
            }

            var ghJson = new JObject
            {
                ["components"] = JArray.FromObject(componentSpecs),
                ["connections"] = new JArray() // Empty connections array
            };

            return ghJson;
        }
    }
}
