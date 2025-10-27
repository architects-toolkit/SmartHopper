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
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Serializes Grasshopper canvas objects to GhJSON format.
    /// Extracts components, connections, and groups into a structured document.
    /// </summary>
    public static class GhJsonSerializer
    {
        /// <summary>
        /// Serializes Grasshopper objects to a GhJSON document.
        /// </summary>
        /// <param name="objects">Objects to serialize</param>
        /// <param name="options">Serialization options (null uses Standard)</param>
        /// <returns>GhJSON document representation</returns>
        public static GrasshopperDocument Serialize(
            IEnumerable<IGH_ActiveObject> objects,
            SerializationOptions options = null)
        {
            options ??= SerializationOptions.Standard;

            // Create property manager if not provided
            var propertyManager = options.PropertyManager ?? new PropertyManagerV2(options.Context);

            return SerializeWithManager(objects, propertyManager, options);
        }

        /// <summary>
        /// Serializes with a specific property manager.
        /// </summary>
        private static GrasshopperDocument SerializeWithManager(
            IEnumerable<IGH_ActiveObject> objects,
            PropertyManagerV2 propertyManager,
            SerializationOptions options)
        {
            var objectsList = objects.ToList();

            var document = new GrasshopperDocument
            {
                Components = ExtractComponents(objectsList, propertyManager, options)
            };

            // Assign sequential IDs if using compact representation
            if (options.UseCompactIds)
            {
                AssignComponentIds(document);
            }

            // Extract connections
            if (options.IncludeConnections)
            {
                document.Connections = ExtractConnections(objectsList, document);
            }

            // Extract metadata
            if (options.IncludeMetadata)
            {
                document.Metadata = CreateDocumentMetadata(objectsList);
            }

            // Extract groups
            if (options.IncludeGroups)
            {
                ExtractGroupInformation(document);
            }

            return document;
        }

        #region Component Extraction

        /// <summary>
        /// Extracts component properties from Grasshopper objects.
        /// </summary>
        private static List<ComponentProperties> ExtractComponents(
            List<IGH_ActiveObject> objects,
            PropertyManagerV2 propertyManager,
            SerializationOptions options)
        {
            var components = new List<ComponentProperties>();

            foreach (var obj in objects.OfType<IGH_Component>())
            {
                try
                {
                    var component = CreateComponentProperties(obj, propertyManager, options);
                    if (component != null)
                    {
                        components.Add(component);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Error extracting component {obj.Name}: {ex.Message}");
                }
            }

            return components;
        }

        /// <summary>
        /// Creates ComponentProperties from a Grasshopper component.
        /// </summary>
        private static ComponentProperties CreateComponentProperties(
            IGH_ActiveObject obj,
            PropertyManagerV2 propertyManager,
            SerializationOptions options)
        {
            var component = new ComponentProperties
            {
                InstanceGuid = obj.InstanceGuid,
                ComponentGuid = obj.ComponentGuid,
                Library = obj.Category,
                Type = obj.SubCategory,
                Name = obj.Name,
                NickName = obj.NickName,
                Pivot = new CompactPosition(obj.Attributes.Pivot.X, obj.Attributes.Pivot.Y)
            };

            // Only include Selected when true (avoid irrelevant false values)
            if (obj.Attributes.Selected)
            {
                component.Selected = true;
            }

            // Extract runtime warnings and errors only if they exist (avoid empty arrays)
            var warnings = obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToList();
            var errors = obj.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToList();

            if (warnings.Any())
                component.Warnings = warnings;
            if (errors.Any())
                component.Errors = errors;

            // Extract schema properties (filtered by property manager)
            ExtractSchemaProperties(component, obj, propertyManager);

            // Extract basic parameters
            component.Params = ExtractBasicParams(obj, propertyManager);

            // Extract parameter settings
            if (obj is IGH_Component ghComponent)
            {
                component.InputSettings = ExtractParameterSettings(
                    ghComponent.Params.Input, propertyManager, ghComponent, options);
                component.OutputSettings = ExtractParameterSettings(
                    ghComponent.Params.Output, propertyManager, ghComponent, options);
            }

            // Extract component state (includes universal value)
            if (options.ExtractComponentState)
            {
                component.ComponentState = ExtractComponentState(component, obj, propertyManager);
            }

            return component;
        }

        /// <summary>
        /// Extracts schema properties using the property manager.
        /// </summary>
        private static void ExtractSchemaProperties(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            var extractedProps = propertyManager.ExtractProperties(originalObject);
            if (extractedProps != null && extractedProps.Count > 0)
            {
                // Convert to Dictionary<string, object>
                component.SchemaProperties = extractedProps.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value);
            }
        }

        /// <summary>
        /// Extracts basic parameters using property manager.
        /// Uses DataTypeSerializer for complex types like Color, Point3d, etc.
        /// </summary>
        private static Dictionary<string, object> ExtractBasicParams(
            IGH_ActiveObject obj,
            PropertyManagerV2 propertyManager)
        {
            var basicParams = new Dictionary<string, object>();

            // Get all instance properties
            var properties = obj.GetType().GetProperties();

            foreach (var prop in properties)
            {
                if (propertyManager.ShouldIncludeProperty(prop.Name, obj))
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        if (value != null)
                        {
                            // Use DataTypeSerializer for complex types to ensure proper formatting
                            if (DataTypeSerializer.IsTypeSupported(value.GetType()))
                            {
                                basicParams[prop.Name] = DataTypeSerializer.Serialize(value);
                            }
                            else
                            {
                                basicParams[prop.Name] = value;
                            }
                        }
                    }
                    catch
                    {
                        // Skip properties that throw on access
                    }
                }
            }

            return basicParams.Count > 0 ? basicParams : null;
        }

        #endregion

        #region Parameter Extraction

        /// <summary>
        /// Extracts parameter settings from a collection of parameters.
        /// </summary>
        private static List<ParameterSettings> ExtractParameterSettings(
            IEnumerable<IGH_Param> parameters,
            PropertyManagerV2 propertyManager,
            IGH_Component component,
            SerializationOptions options)
        {
            var settings = new List<ParameterSettings>();
            var paramList = parameters.ToList();

            for (int i = 0; i < paramList.Count; i++)
            {
                var param = paramList[i];
                bool isPrincipal = (component is IGH_Component ghComp && ghComp.Params.IndexOfInputParam(param.Name) == 0);

                var paramSettings = CreateParameterSettings(param, component, isPrincipal, options);
                if (paramSettings != null)
                {
                    settings.Add(paramSettings);
                }
            }

            return settings.Count > 0 ? settings : null;
        }

        /// <summary>
        /// Creates parameter settings for a single parameter.
        /// </summary>
        private static ParameterSettings CreateParameterSettings(
            IGH_Param param,
            IGH_Component owner,
            bool isPrincipal,
            SerializationOptions options)
        {
            // Check if owner is a script component
            if (owner is IScriptComponent scriptComp)
            {
                return ScriptParameterMapper.ExtractSettings(param, scriptComp, isPrincipal);
            }
            else
            {
                // Use generic parameter mapper for non-script components
                if (ParameterMapper.HasCustomSettings(param, isPrincipal))
                {
                    return ParameterMapper.ExtractSettings(param, isPrincipal);
                }
            }

            return null;
        }

        #endregion

        #region State Extraction

        /// <summary>
        /// Extracts component state (enabled, locked, hidden, etc.).
        /// </summary>
        private static ComponentState ExtractComponentState(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            var state = new ComponentState();
            bool hasState = false;

            // Extract Enabled state
            if (!originalObject.Locked && propertyManager.ShouldIncludeProperty("Enabled", originalObject))
            {
                state.Enabled = true;
                hasState = true;
            }

            // Extract Locked state
            if (originalObject.Locked && propertyManager.ShouldIncludeProperty("Locked", originalObject))
            {
                state.Locked = true;
                hasState = true;
            }

            // Extract Hidden and Preview state for components
            if (originalObject is IGH_Component ghComponent)
            {
                if (ghComponent.Hidden && propertyManager.ShouldIncludeProperty("Hidden", originalObject))
                {
                    state.Hidden = true;
                    hasState = true;
                }

                if (!ghComponent.Hidden && propertyManager.ShouldIncludeProperty("Preview", originalObject))
                {
                    state.Preview = !ghComponent.Hidden;
                    hasState = true;
                }
            }

            // Extract universal value for special components
            var universalValue = ExtractUniversalValue(originalObject, propertyManager);
            if (universalValue != null)
            {
                state.Value = universalValue;
                hasState = true;
            }

            return hasState ? state : null;
        }

        #endregion

        #region Universal Value Extraction

        /// <summary>
        /// Extracts universal value for special components (sliders, panels, etc.).
        /// </summary>
        private static object ExtractUniversalValue(
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            if (!propertyManager.ShouldIncludeProperty("UniversalValue", originalObject))
                return null;

            try
            {
                // Number slider
                if (originalObject is GH_NumberSlider slider)
                {
                    return FormatSliderValue(slider);
                }

                // Panel
                if (originalObject is GH_Panel panel)
                {
                    return panel.UserText;
                }

                // Value list
                if (originalObject is GH_ValueList valueList)
                {
                    return valueList.FirstSelectedItem?.Name;
                }

                // Boolean toggle
                if (originalObject is GH_BooleanToggle toggle)
                {
                    return toggle.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Error extracting universal value: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Formats a slider value with appropriate precision.
        /// </summary>
        private static string FormatSliderValue(GH_NumberSlider slider)
        {
            var decimals = slider.Slider.DecimalPlaces;
            
            if (decimals == 0)
                return slider.CurrentValue.ToString("F0");
            
            return slider.CurrentValue.ToString($"F{decimals}");
        }

        #endregion

        #region Connection Extraction

        /// <summary>
        /// Extracts wire connections between components.
        /// </summary>
        private static List<ConnectionPairing> ExtractConnections(
            List<IGH_ActiveObject> objects,
            GrasshopperDocument document)
        {
            var connections = new List<ConnectionPairing>();
            
            // Build GUID to ID mapping if using compact IDs
            var guidToId = new Dictionary<Guid, int>();
            if (document.Components != null)
            {
                foreach (var comp in document.Components)
                {
                    if (comp.Id.HasValue)
                    {
                        guidToId[comp.InstanceGuid] = comp.Id.Value;
                    }
                }
            }

            foreach (var obj in objects.OfType<IGH_Component>())
            {
                foreach (var outputParam in obj.Params.Output)
                {
                    foreach (var recipient in outputParam.Recipients)
                    {
                        var targetComp = recipient.Attributes?.GetTopLevel?.DocObject as IGH_Component;
                        if (targetComp != null)
                        {
                            var connection = new ConnectionPairing
                            {
                                From = new Connection
                                {
                                    Id = guidToId.ContainsKey(obj.InstanceGuid) 
                                        ? guidToId[obj.InstanceGuid] 
                                        : -1,
                                    ParamName = outputParam.Name
                                },
                                To = new Connection
                                {
                                    Id = guidToId.ContainsKey(targetComp.InstanceGuid)
                                        ? guidToId[targetComp.InstanceGuid]
                                        : -1,
                                    ParamName = recipient.Name
                                }
                            };

                            connections.Add(connection);
                        }
                    }
                }
            }

            return connections.Count > 0 ? connections : null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Assigns sequential IDs to components for compact representation.
        /// </summary>
        private static void AssignComponentIds(GrasshopperDocument document)
        {
            if (document.Components != null)
            {
                for (int i = 0; i < document.Components.Count; i++)
                {
                    document.Components[i].Id = i;
                }
            }
        }

        /// <summary>
        /// Creates document metadata.
        /// </summary>
        private static DocumentMetadata CreateDocumentMetadata(List<IGH_ActiveObject> objects)
        {
            return new DocumentMetadata
            {
                CreatedAt = DateTime.UtcNow.ToString("o"),
                GrasshopperVersion = Instances.Settings.GetValue("AssemblyVersion", "Unknown"),
                ComponentCount = objects.OfType<IGH_Component>().Count(),
                PluginVersion = typeof(GhJsonSerializer).Assembly.GetName().Version?.ToString()
            };
        }

        /// <summary>
        /// Extracts group information from the canvas.
        /// </summary>
        private static void ExtractGroupInformation(GrasshopperDocument document)
        {
            try
            {
                var canvas = Instances.ActiveCanvas;
                if (canvas?.Document == null)
                    return;

                var groups = canvas.Document.Objects.OfType<GH_Group>().ToList();
                if (groups.Count == 0)
                    return;

                // Build GUID to ID mapping
                var guidToId = new Dictionary<Guid, int>();
                if (document.Components != null)
                {
                    foreach (var comp in document.Components)
                    {
                        if (comp.Id.HasValue)
                        {
                            guidToId[comp.InstanceGuid] = comp.Id.Value;
                        }
                    }
                }

                document.Groups = new List<GroupInfo>();

                foreach (var group in groups)
                {
                    var groupInfo = new GroupInfo
                    {
                        Name = group.NickName,
                        Color = DataTypeSerializer.Serialize(group.Colour)
                    };

                    // Map member GUIDs to IDs
                    var memberIds = new List<int>();
                    foreach (var member in group.Objects())
                    {
                        // Current enumeration yields IGH_DocumentObject; get InstanceGuid
                        var objDo = member as IGH_DocumentObject;
                        if (objDo == null)
                            continue;

                        var guid = objDo.InstanceGuid;
                        if (guidToId.TryGetValue(guid, out var id))
                        {
                            memberIds.Add(id);
                        }
                    }

                    groupInfo.Members = memberIds;
                    document.Groups.Add(groupInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Error extracting groups: {ex.Message}");
            }
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Factory methods for common serialization scenarios.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Serializes with Standard format (default).
            /// Balanced, clean structure for AI processing with all essential data.
            /// </summary>
            public static GrasshopperDocument Standard(IEnumerable<IGH_ActiveObject> objects)
            {
                return Serialize(objects, SerializationOptions.Standard);
            }

            /// <summary>
            /// Serializes with Lite format.
            /// Compressed variant optimized for minimal token usage.
            /// </summary>
            public static GrasshopperDocument Lite(IEnumerable<IGH_ActiveObject> objects)
            {
                return Serialize(objects, SerializationOptions.Lite);
            }
        }

        #endregion
    }
}
