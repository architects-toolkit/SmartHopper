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
using System.Drawing;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    /// <summary>
    /// Modern document introspection system that uses the new property management architecture.
    /// Provides clean, maintainable extraction of Grasshopper objects with flexible filtering.
    /// </summary>
    public static class DocumentIntrospectionV2
    {
        /// <summary>
        /// Extracts comprehensive details of Grasshopper objects using the new property system.
        /// </summary>
        /// <param name="objects">The objects to extract details from.</param>
        /// <param name="context">Serialization context that determines which properties to include.</param>
        /// <param name="includeMetadata">Whether to include document metadata.</param>
        /// <param name="includeGroups">Whether to include group information.</param>
        /// <returns>GhJSON document with extracted object details.</returns>
        public static GrasshopperDocument ExtractDocument(
            IEnumerable<IGH_ActiveObject> objects,
            SerializationContext context = SerializationContext.Standard,
            bool includeMetadata = false,
            bool includeGroups = true)
        {
            var propertyManager = new PropertyManagerV2(context);
            return ExtractDocumentWithManager(objects, propertyManager, includeMetadata, includeGroups);
        }

        /// <summary>
        /// Extracts document details using a custom property manager.
        /// </summary>
        /// <param name="objects">The objects to extract details from.</param>
        /// <param name="propertyManager">Custom property manager with specific filtering rules.</param>
        /// <param name="includeMetadata">Whether to include document metadata.</param>
        /// <param name="includeGroups">Whether to include group information.</param>
        /// <returns>GhJSON document with extracted object details.</returns>
        public static GrasshopperDocument ExtractDocumentWithManager(
            IEnumerable<IGH_ActiveObject> objects,
            PropertyManagerV2 propertyManager,
            bool includeMetadata = false,
            bool includeGroups = true)
        {
            var objectList = objects.ToList();
            var document = new GrasshopperDocument();

            // Extract components
            document.Components = ExtractComponents(objectList, propertyManager);

            // Assign sequential IDs first
            AssignComponentIds(document);

            // Extract connections (will use integer IDs)
            document.Connections = ExtractConnections(objectList, document);

            // Include metadata if requested
            if (includeMetadata)
            {
                document.Metadata = CreateDocumentMetadata(objectList);
            }

            // Include groups if requested
            if (includeGroups)
            {
                ExtractGroupInformation(document);
            }

            return document;
        }

        /// <summary>
        /// Extracts components using the modern property management system.
        /// </summary>
        /// <param name="objects">Objects to extract components from.</param>
        /// <param name="propertyManager">Property manager for filtering and extraction.</param>
        /// <returns>List of extracted component properties.</returns>
        private static List<ComponentProperties> ExtractComponents(
            IEnumerable<IGH_ActiveObject> objects,
            PropertyManagerV2 propertyManager)
        {
            var components = new List<ComponentProperties>();

            foreach (var obj in objects)
            {
                try
                {
                    var component = CreateComponentProperties(obj, propertyManager);
                    if (component != null)
                    {
                        components.Add(component);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error extracting component {obj.Name}: {ex.Message}");
                }
            }

            return components;
        }

        /// <summary>
        /// Creates ComponentProperties for a single object using the property manager.
        /// </summary>
        /// <param name="obj">The Grasshopper object to extract from.</param>
        /// <param name="propertyManager">Property manager for filtering and extraction.</param>
        /// <returns>ComponentProperties instance with extracted data.</returns>
        private static ComponentProperties CreateComponentProperties(
            IGH_ActiveObject obj,
            PropertyManagerV2 propertyManager)
        {
            var component = new ComponentProperties
            {
                Name = obj.Name,
                ComponentGuid = obj.ComponentGuid,
                InstanceGuid = obj.InstanceGuid,
                Pivot = new CompactPosition(obj.Attributes.Pivot.X, obj.Attributes.Pivot.Y)
            };

            // Only include Selected when true (avoid irrelevant false values)
            if (obj.Attributes.Selected)
            {
                component.Selected = true;
            }
            // Selected remains null when false, which will be omitted from JSON

            // Extract properties using the property manager
            component.Properties = propertyManager.ExtractProperties(obj);

            // Extract schema-specific properties
            ExtractSchemaProperties(component, obj, propertyManager);

            // Extract warnings and errors only if they exist (avoid empty arrays)
            var warnings = obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToList();
            var errors = obj.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToList();
            
            if (warnings.Any())
                component.Warnings = warnings;
            if (errors.Any())
                component.Errors = errors;

            return component;
        }

        /// <summary>
        /// Extracts modern schema properties (inputSettings, outputSettings, componentState, etc.).
        /// </summary>
        /// <param name="component">Component to populate with schema properties.</param>
        /// <param name="originalObject">Original Grasshopper object.</param>
        /// <param name="propertyManager">Property manager for extraction.</param>
        private static void ExtractSchemaProperties(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            // Extract basic params (NickName, etc.)
            var basicParams = ExtractBasicParams(originalObject, propertyManager);
            if (basicParams.Any())
            {
                component.Params = basicParams;
            }

            // Extract input/output settings for components
            if (originalObject is IGH_Component ghComponent)
            {
                component.InputSettings = ExtractParameterSettings(ghComponent.Params.Input, propertyManager, ghComponent);
                component.OutputSettings = ExtractParameterSettings(ghComponent.Params.Output, propertyManager, ghComponent);
            }

            // Extract component state
            var componentState = ExtractComponentState(component, originalObject, propertyManager);
            if (componentState != null)
            {
                component.ComponentState = componentState;
            }
        }

        /// <summary>
        /// Extracts basic parameters like NickName, avoiding redundant data.
        /// </summary>
        /// <param name="obj">Object to extract from.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <returns>Dictionary of basic parameter values.</returns>
        private static Dictionary<string, object> ExtractBasicParams(
            IGH_ActiveObject obj,
            PropertyManagerV2 propertyManager)
        {
            var basicParams = new Dictionary<string, object>();

            // Only include NickName if it differs from Name (avoid redundancy)
            if (propertyManager.ShouldIncludeProperty("NickName", obj))
            {
                var nickName = obj.NickName;
                if (!string.IsNullOrEmpty(nickName) && nickName != obj.Name)
                {
                    basicParams["NickName"] = nickName;
                }
            }

            return basicParams;
        }

        /// <summary>
        /// Extracts parameter settings using the property manager.
        /// </summary>
        /// <param name="parameters">Parameters to extract settings from.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <param name="component">Optional component to check for principal parameter index.</param>
        /// <returns>List of parameter settings.</returns>
        private static List<ParameterSettings> ExtractParameterSettings(
            IEnumerable<IGH_Param> parameters,
            PropertyManagerV2 propertyManager,
            IGH_Component component = null)
        {
            var settings = new List<ParameterSettings>();
            var paramList = parameters.ToList();
            
            // Get principal parameter index from component (only for input parameters)
            int principalIndex = -1;
            if (component is GH_Component ghComp && paramList.Count > 0)
            {
                // Check if this is the input parameter list
                var firstParam = paramList.FirstOrDefault();
                if (firstParam != null && ghComp.Params.Input.Contains(firstParam))
                {
                    // Use the component's PrincipalParameterIndex if set
                    System.Diagnostics.Debug.WriteLine($"[ExtractParameterSettings] Component '{ghComp.Name}' PrincipalParameterIndex: {ghComp.PrincipalParameterIndex}, Input count: {paramList.Count}");
                    if (ghComp.PrincipalParameterIndex >= 0 && ghComp.PrincipalParameterIndex < paramList.Count)
                    {
                        principalIndex = ghComp.PrincipalParameterIndex;
                        System.Diagnostics.Debug.WriteLine($"[ExtractParameterSettings] Setting principal parameter at index {principalIndex} for '{ghComp.Name}'");
                    }
                }
            }

            for (int i = 0; i < paramList.Count; i++)
            {
                var param = paramList[i];
                bool isPrincipal = (i == principalIndex);
                var paramSettings = CreateParameterSettings(param, propertyManager, isPrincipal);
                if (paramSettings != null)
                {
                    settings.Add(paramSettings);
                }
            }

            return settings;
        }

        /// <summary>
        /// Creates ParameterSettings for a single parameter using the property manager.
        /// </summary>
        /// <param name="param">Parameter to extract settings from.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <param name="isPrincipal">Whether this parameter is the principal parameter (from component.PrincipalParameterIndex).</param>
        /// <returns>ParameterSettings instance or null if no relevant settings.</returns>
        private static ParameterSettings CreateParameterSettings(
            IGH_Param param,
            PropertyManagerV2 propertyManager,
            bool isPrincipal = false)
        {
            var settings = new ParameterSettings
            {
                ParameterName = param.Name
            };

            bool hasSettings = false;

            // Extract data mapping if allowed
            if (propertyManager.ShouldIncludeProperty("DataMapping", param) && 
                param.DataMapping != GH_DataMapping.None)
            {
                settings.DataMapping = param.DataMapping.ToString();
                hasSettings = true;
            }

            // Extract expression only if it actually exists and is not empty
            if (propertyManager.ShouldIncludeProperty("Expression", param))
            {
                var expression = propertyManager.ExtractProperty(param, "Expression");
                if (expression?.Value is string expressionStr && !string.IsNullOrEmpty(expressionStr))
                {
                    // Only set Expression - HasExpression is redundant and will be removed
                    settings.Expression = expressionStr;
                    hasSettings = true;
                }
                // HasExpression flag is redundant - presence of Expression property implies it has one
            }

            // Extract additional settings
            var additionalSettings = ExtractAdditionalParameterSettings(param, propertyManager, isPrincipal);
            if (additionalSettings != null)
            {
                settings.AdditionalSettings = additionalSettings;
                hasSettings = true;
            }

            return hasSettings ? settings : null;
        }

        /// <summary>
        /// Extracts additional parameter settings using the property manager.
        /// </summary>
        /// <param name="param">Parameter to extract from.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <param name="isPrincipal">Whether this parameter is the principal parameter (from component.PrincipalParameterIndex).</param>
        /// <returns>AdditionalParameterSettings or null if no relevant settings.</returns>
        private static AdditionalParameterSettings ExtractAdditionalParameterSettings(
            IGH_Param param,
            PropertyManagerV2 propertyManager,
            bool isPrincipal = false)
        {
            var additionalSettings = new AdditionalParameterSettings();
            bool hasAdditionalSettings = false;

            // Only include flags when they're true (avoid irrelevant false values)
            if (propertyManager.ShouldIncludeProperty("Reverse", param) && param.Reverse)
            {
                additionalSettings.Reverse = true;
                hasAdditionalSettings = true;
            }

            if (propertyManager.ShouldIncludeProperty("Simplify", param) && param.Simplify)
            {
                additionalSettings.Simplify = true;
                hasAdditionalSettings = true;
            }

            if (propertyManager.ShouldIncludeProperty("Locked", param) && param.Locked)
            {
                additionalSettings.Locked = true;
                hasAdditionalSettings = true;
            }

            // Set IsPrincipal based on component's PrincipalParameterIndex
            // This is managed at the component level, not at the parameter level
            if (isPrincipal && propertyManager.ShouldIncludeProperty("IsPrincipal", param))
            {
                additionalSettings.IsPrincipal = true;
                hasAdditionalSettings = true;
                System.Diagnostics.Debug.WriteLine($"[ExtractAdditionalParameterSettings] Added isPrincipal=true for parameter '{param.Name}'");
            }
            else if (isPrincipal)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtractAdditionalParameterSettings] isPrincipal detected for '{param.Name}' but ShouldIncludeProperty returned false");
            }

            return hasAdditionalSettings ? additionalSettings : null;
        }

        /// <summary>
        /// Extracts component state using the property manager.
        /// </summary>
        /// <param name="component">Component properties being built.</param>
        /// <param name="originalObject">Original Grasshopper object.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <returns>ComponentState or null if no relevant state.</returns>
        private static ComponentState ExtractComponentState(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            var state = new ComponentState();
            bool hasState = false;

            // Extract component-level properties only when they're not default values
            if (originalObject is GH_Component ghComponent)
            {
                if (propertyManager.ShouldIncludeProperty("Locked", ghComponent) && ghComponent.Locked)
                {
                    state.Locked = true;
                    hasState = true;
                }

                if (propertyManager.ShouldIncludeProperty("Hidden", ghComponent) && ghComponent.Hidden)
                {
                    state.Hidden = true;
                    hasState = true;
                }
            }

            // Extract value consolidation based on component type and property manager rules
            var valueProperty = ExtractUniversalValue(component, originalObject, propertyManager);
            if (valueProperty != null)
            {
                state.Value = valueProperty;
                hasState = true;
            }

            return hasState ? state : null;
        }

        /// <summary>
        /// Extracts the universal value property and removes duplicate from Properties to avoid redundancy.
        /// </summary>
        /// <param name="component">Component properties being built.</param>
        /// <param name="originalObject">Original Grasshopper object.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <returns>Universal value string or null.</returns>
        private static string ExtractUniversalValue(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            // Value consolidation mapping based on component type
            string value = originalObject switch
            {
                GH_NumberSlider when propertyManager.ShouldIncludeProperty("CurrentValue", originalObject) &&
                                   component.Properties.ContainsKey("CurrentValue") =>
                    component.Properties["CurrentValue"].Value?.ToString(),

                GH_Panel when propertyManager.ShouldIncludeProperty("UserText", originalObject) &&
                             component.Properties.ContainsKey("UserText") =>
                    component.Properties["UserText"].Value?.ToString(),

                GH_Scribble when propertyManager.ShouldIncludeProperty("Text", originalObject) &&
                                component.Properties.ContainsKey("Text") =>
                    component.Properties["Text"].Value?.ToString(),

                _ when propertyManager.ShouldIncludeProperty("ListItems", originalObject) &&
                      component.Properties.ContainsKey("ListItems") =>
                    component.Properties["ListItems"].Value?.ToString(),

                _ => null
            };

            // Remove the duplicate property from Properties to avoid redundancy
            if (!string.IsNullOrEmpty(value))
            {
                var propertyToRemove = originalObject switch
                {
                    GH_NumberSlider => "CurrentValue",
                    GH_Panel => "UserText",
                    GH_Scribble => "Text",
                    _ when component.Properties.ContainsKey("ListItems") => "ListItems",
                    _ => null
                };

                if (propertyToRemove != null && component.Properties.ContainsKey(propertyToRemove))
                {
                    component.Properties.Remove(propertyToRemove);
                }
            }

            return value;
        }

        /// <summary>
        /// Extracts connections between components using existing connection models.
        /// Uses integer IDs from the document instead of GUIDs for compact representation.
        /// </summary>
        private static List<ConnectionPairing> ExtractConnections(IEnumerable<IGH_ActiveObject> objects, GrasshopperDocument document)
        {
            var connections = new List<ConnectionPairing>();
            
            // Create GUID -> ID mapping for efficient lookups
            var guidToId = document.Components.ToDictionary(c => c.InstanceGuid, c => c.Id);

            foreach (var obj in objects)
            {
                if (obj is IGH_Component component)
                {
                    foreach (var outputParam in component.Params.Output)
                    {
                        foreach (var recipient in outputParam.Recipients)
                        {
                            var fromGuid = component.InstanceGuid;
                            var toGuid = recipient.Attributes.GetTopLevel.DocObject.InstanceGuid;

                            // Only add connection if both components are in the document and have valid IDs
                            if (guidToId.TryGetValue(fromGuid, out var fromId) && 
                                guidToId.TryGetValue(toGuid, out var toId) &&
                                fromId.HasValue && toId.HasValue)
                            {
                                connections.Add(new ConnectionPairing
                                {
                                    From = new Connection
                                    {
                                        Id = fromId.Value,
                                        ParamName = outputParam.Name,
                                    },
                                    To = new Connection
                                    {
                                        Id = toId.Value,
                                        ParamName = recipient.Name,
                                    },
                                });
                            }
                        }
                    }
                }
                else if (obj is IGH_Param param)
                {
                    // Process parameter outputs
                    foreach (var recipient in param.Recipients)
                    {
                        var fromGuid = param.InstanceGuid;
                        var recipientGuid = recipient.Attributes?.GetTopLevel?.DocObject?.InstanceGuid ?? Guid.Empty;
                        
                        // Only add connection if both components are in the document and have valid IDs
                        if (recipientGuid != Guid.Empty && 
                            guidToId.TryGetValue(fromGuid, out var fromId) && 
                            guidToId.TryGetValue(recipientGuid, out var toId) &&
                            fromId.HasValue && toId.HasValue)
                        {
                            connections.Add(new ConnectionPairing
                            {
                                From = new Connection
                                {
                                    Id = fromId.Value,
                                    ParamName = param.Name
                                },
                                To = new Connection
                                {
                                    Id = toId.Value,
                                    ParamName = recipient.Name
                                }
                            });
                        }
                    }
                }
            }

            return connections;
        }

        /// <summary>
        /// Assigns sequential IDs to components (unchanged from original implementation).
        /// </summary>
        private static void AssignComponentIds(GrasshopperDocument document)
        {
            if (document.Components != null)
            {
                for (int i = 0; i < document.Components.Count; i++)
                {
                    document.Components[i].Id = i + 1;
                }
            }
        }

        /// <summary>
        /// Creates document metadata (unchanged from original implementation).
        /// </summary>
        private static DocumentMetadata CreateDocumentMetadata(IEnumerable<IGH_ActiveObject> objects)
        {
            // Implementation unchanged from original DocumentIntrospection
            var metadata = new DocumentMetadata
            {
                Created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Modified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Author = "SmartHopper AI" // TODO: assign author
            };

            // Add version information and dependencies as in original implementation
            return metadata;
        }

        /// <summary>
        /// Extracts group information from the canvas and filters members.
        /// Uses integer IDs instead of GUIDs for compact representation.
        /// </summary>
        private static void ExtractGroupInformation(GrasshopperDocument document)
        {
            try
            {
                var canvas = CanvasAccess.GetCurrentCanvas();
                if (canvas == null)
                {
                    return;
                }

                var groups = new List<GroupInfo>();
                
                // Create GUID -> ID mapping for efficient lookups (same as connections)
                var guidToId = document.Components
                    .Where(c => c.Id.HasValue)
                    .ToDictionary(c => c.InstanceGuid, c => c.Id.Value);

                // Extract all groups from the canvas
                foreach (var obj in canvas.Objects)
                {
                    if (obj is GH_Group group)
                    {
                        // Filter members to only include those in the document, and map to IDs
                        var filteredMemberIds = new List<int>();
                        foreach (var memberObj in group.Objects())
                        {
                            var memberGuid = memberObj.InstanceGuid;
                            if (guidToId.TryGetValue(memberGuid, out var memberId))
                            {
                                filteredMemberIds.Add(memberId);
                            }
                        }

                        // Only include groups that have at least one member in the document
                        if (filteredMemberIds.Count > 0)
                        {
                            var groupInfo = new GroupInfo
                            {
                                InstanceGuid = group.InstanceGuid,
                                Members = filteredMemberIds
                            };

                            // Include name if it's not the default
                            if (!string.IsNullOrEmpty(group.NickName) && group.NickName != "Group")
                            {
                                groupInfo.Name = group.NickName;
                            }

                            // Include color in ARGB format
                            var color = group.Colour;
                            groupInfo.Color = $"{color.A},{color.R},{color.G},{color.B}";

                            groups.Add(groupInfo);
                        }
                    }
                }

                // Only set Groups if we found any
                if (groups.Count > 0)
                {
                    document.Groups = groups;
                    System.Diagnostics.Debug.WriteLine($"[ExtractGroupInformation] Extracted {groups.Count} groups with {groups.Sum(g => g.Members.Count)} total members");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtractGroupInformation] Error extracting groups: {ex.Message}");
            }
        }

        /// <summary>
        /// Factory methods for common extraction scenarios.
        /// </summary>
        public static class ExtractionFactory
        {
            /// <summary>
            /// Extracts document with standard format (default).
            /// </summary>
            public static GrasshopperDocument ForStandard(IEnumerable<IGH_ActiveObject> objects)
            {
                return ExtractDocument(objects, SerializationContext.Standard, false, true);
            }

            /// <summary>
            /// Extracts document with lite format (compressed for AI).
            /// </summary>
            public static GrasshopperDocument ForLite(IEnumerable<IGH_ActiveObject> objects)
            {
                return ExtractDocument(objects, SerializationContext.Lite, false, false);
            }

            /// <summary>
            /// Extracts document with custom property filtering.
            /// </summary>
            public static GrasshopperDocument WithCustomFilter(
                IEnumerable<IGH_ActiveObject> objects,
                PropertyFilterRule customRule)
            {
                var propertyManager = PropertyManagerV2.CreateCustom(customRule);
                return ExtractDocumentWithManager(objects, propertyManager, false, true);
            }
        }

        /// <summary>
        /// Groups Grasshopper objects by GUID with undo support.
        /// </summary>
        /// <param name="guids">List of GUIDs to include in the group.</param>
        /// <param name="groupName">Optional name for the group.</param>
        /// <param name="color">Optional color for the group.</param>
        /// <returns>The created group object.</returns>
        public static IGH_DocumentObject GroupObjects(IList<Guid> guids, string groupName = null, System.Drawing.Color? color = null)
        {
            GH_Document doc = CanvasAccess.GetCurrentCanvas();
            GH_Group group = new GH_Group();

            // Set group name if provided
            if (!string.IsNullOrEmpty(groupName))
            {
                group.NickName = groupName;
            }

            // Set group color (use provided color or default green)
            group.Colour = color ?? System.Drawing.Color.FromArgb(255, 0, 200, 0);
            System.Diagnostics.Debug.WriteLine($"[GroupObjects] Setting group color to ARGB({group.Colour.A},{group.Colour.R},{group.Colour.G},{group.Colour.B})");

            // Add objects to group
            foreach (var guid in guids)
            {
                var obj = CanvasAccess.FindInstance(guid);
                if (obj != null)
                {
                    group.AddObject(guid);
                }
            }

            // Add the group to the document WITHOUT undo support to prevent infinite loops
            // Setting the second parameter to false prevents document change events from firing
            // Note: Canvas refresh will be handled by the caller after all operations complete
            doc.AddObject(group, false);

            return group;
        }

        /// <summary>
        /// Legacy compatibility method that maps to the new ExtractDocument method.
        /// Extracts comprehensive details of Grasshopper objects.
        /// </summary>
        /// <param name="objects">The objects to extract details from.</param>
        /// <param name="includeMetadata">Whether to include document metadata.</param>
        /// <param name="includeGroups">Whether to include group information. Default is true.</param>
        /// <returns>A complete GrasshopperDocument with all requested information.</returns>
        public static GrasshopperDocument GetObjectsDetails(IEnumerable<IGH_ActiveObject> objects, bool includeMetadata, bool includeGroups = true)
        {
            return ExtractDocument(objects, SerializationContext.Standard, includeMetadata, includeGroups);
        }
    }
}
