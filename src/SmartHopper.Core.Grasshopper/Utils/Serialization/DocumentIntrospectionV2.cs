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
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Internal;
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

            // Extract schema-specific properties (params, inputSettings, outputSettings, componentState)
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
                var paramSettings = CreateParameterSettings(param, propertyManager, isPrincipal, component);
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
        /// <param name="component">Optional component that owns this parameter (used to detect script components).</param>
        /// <returns>ParameterSettings instance or null if no relevant settings.</returns>
        private static ParameterSettings CreateParameterSettings(
            IGH_Param param,
            PropertyManagerV2 propertyManager,
            bool isPrincipal = false,
            IGH_Component component = null)
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

            // Extract variable name and type hint for script component parameters
            // Script parameters use NickName as the variable name
            if (component is IScriptComponent scriptComp)
            {
                var variableName = param.NickName;
                if (!string.IsNullOrEmpty(variableName))
                {
                    // Unsanitize C# identifiers to ensure consistent JSON storage
                    // This reverses the sanitization done during component placement
                    if (ScriptComponentHelper.IsCSharpScriptComponent(scriptComp))
                    {
                        variableName = CSharpIdentifierHelper.UnsanitizeIdentifier(variableName);
                        if (!string.Equals(param.NickName, variableName, StringComparison.Ordinal))
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExtractVariableName] ✓ Unsanitized variable name '{param.NickName}' -> '{variableName}' for C# script component");
                        }
                    }

                    settings.VariableName = variableName;
                    hasSettings = true;
                    System.Diagnostics.Debug.WriteLine($"[ExtractVariableName] ✓ Extracted variable name '{variableName}' from parameter '{param.Name}'");
                }

                // Extract type hint from script code signature (C#) or infer from parameter type (Python/VB)
                // ScriptVariableParam has no TypeHint property - type info is stored differently per language
                try
                {
                    if (scriptComp != null && !string.IsNullOrEmpty(variableName))
                    {
                        var scriptCode = scriptComp.Text;
                        var isInput = param.Kind == GH_ParamKind.input;

                        // Try to extract from C# signature first
                        var typeHint = ExtractTypeHintFromScriptSignature(scriptCode, variableName, isInput);

                        // If not found in signature (Python/VB scripts), infer from parameter's current type
                        if (string.IsNullOrEmpty(typeHint))
                        {
                            typeHint = InferTypeHintFromParameter(param, scriptComp);
                        }

                        if (!string.IsNullOrEmpty(typeHint))
                        {
                            settings.TypeHint = typeHint;
                            hasSettings = true;
                            System.Diagnostics.Debug.WriteLine($"[ExtractTypeHint] ✓ Extracted type hint '{typeHint}' for parameter '{param.Name}'");
                        }
#if DEBUG
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExtractTypeHint] Could not extract type hint for parameter '{param.Name}'");
                        }
#endif
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExtractTypeHint] Error extracting type hint for parameter '{param.Name}': {ex.Message}");
                }
            }

            // Extract access mode (always extract for all parameters)
            if (param.Access != GH_ParamAccess.item)
            {
                settings.Access = param.Access.ToString();
                hasSettings = true;
                System.Diagnostics.Debug.WriteLine($"[ExtractAccess] ✓ Extracted access mode '{param.Access}' from parameter '{param.Name}'");
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
            var valueProperty = ExtractUniversalValue(originalObject, propertyManager);
            if (valueProperty != null)
            {
                state.Value = valueProperty;
                hasState = true;
            }

            // Extract panel-specific properties
            if (originalObject is GH_Panel panel)
            {
                if (propertyManager.ShouldIncludeProperty("Multiline", panel))
                {
                    state.Multiline = panel.Properties.Multiline;
                    hasState = true;
                }

                if (propertyManager.ShouldIncludeProperty("DrawIndices", panel))
                {
                    state.DrawIndices = panel.Properties.DrawIndices;
                    hasState = true;
                }

                if (propertyManager.ShouldIncludeProperty("DrawPaths", panel))
                {
                    state.DrawPaths = panel.Properties.DrawPaths;
                    hasState = true;
                }

                if (propertyManager.ShouldIncludeProperty("Alignment", panel))
                {
                    state.Alignment = (int)panel.Properties.Alignment;
                    hasState = true;
                }

                if (propertyManager.ShouldIncludeProperty("Wrap", panel))
                {
                    state.Wrap = panel.Properties.Wrap;
                    hasState = true;
                }

                // Extract panel bounds (size)
                if (propertyManager.ShouldIncludeProperty("Bounds", panel) && panel.Attributes != null)
                {
                    var bounds = panel.Attributes.Bounds;
                    state.Bounds = new Dictionary<string, float>
                    {
                        ["width"] = bounds.Width,
                        ["height"] = bounds.Height
                    };
                    hasState = true;
                }
            }

            // Extract value list properties
            if (originalObject is GH_ValueList valueList)
            {
                if (propertyManager.ShouldIncludeProperty("ListMode", valueList))
                {
                    state.ListMode = valueList.ListMode.ToString();
                    hasState = true;
                }

                // Extract selected item indices
                if (propertyManager.ShouldIncludeProperty("SelectedIndices", valueList))
                {
                    var selectedIndices = new List<int>();
                    for (int i = 0; i < valueList.ListItems.Count; i++)
                    {
                        if (valueList.ListItems[i].Selected)
                        {
                            selectedIndices.Add(i);
                        }
                    }
                    if (selectedIndices.Count > 0)
                    {
                        state.SelectedIndices = selectedIndices;
                        hasState = true;
                    }
                }
            }

            // Extract number slider rounding mode
            if (originalObject is GH_NumberSlider sliderForRounding && propertyManager.ShouldIncludeProperty("Rounding", sliderForRounding))
            {
                // GH_NumberSlider.Slider.Type is the rounding mode enum
                state.Rounding = sliderForRounding.Slider.Type.ToString();
                hasState = true;
            }

            // Extract script component properties
            if (originalObject is IScriptComponent scriptComp)
            {
                if (propertyManager.ShouldIncludeProperty("MarshInputs", scriptComp))
                {
                    state.MarshInputs = scriptComp.MarshInputs;
                    hasState = true;
                }

                if (propertyManager.ShouldIncludeProperty("MarshOutputs", scriptComp))
                {
                    state.MarshOutputs = scriptComp.MarshOutputs;
                    hasState = true;
                }
            }

            return hasState ? state : null;
        }

        /// <summary>
        /// Formats a number slider value with decimal precision encoded in the max value.
        /// Examples: "5<2,10>" (no decimals), "5<2,10.0>" (1 decimal), "5<2,10.000>" (3 decimals)
        /// The max value contains the decimal precision; current and min use minimal formatting.
        /// </summary>
        /// <param name="slider">The number slider to format.</param>
        /// <returns>Formatted slider value string.</returns>
        private static string FormatSliderValue(GH_NumberSlider slider)
        {
            var decimals = slider.Slider.DecimalPlaces;
            
            // Format current value and min with minimal decimals (G29 removes trailing zeros)
            var currentValue = slider.CurrentValue.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            var min = slider.Slider.Minimum.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            
            // Format max with explicit decimal places to encode precision
            var maxFormat = decimals == 0 ? "0" : "0." + new string('0', decimals);
            var max = slider.Slider.Maximum.ToString(maxFormat, System.Globalization.CultureInfo.InvariantCulture);
            
            return $"{currentValue}<{min},{max}>";
        }

        /// <summary>
        /// Extracts the universal value property directly from the Grasshopper object.
        /// </summary>
        /// <param name="originalObject">Original Grasshopper object.</param>
        /// <param name="propertyManager">Property manager for filtering.</param>
        /// <returns>Universal value object or null.</returns>
        private static object ExtractUniversalValue(
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
#if DEBUG
            // Debug logging for script components
            if (originalObject is IScriptComponent scriptDebug)
            {
                var shouldInclude = propertyManager.ShouldIncludeProperty("Script", originalObject);
                System.Diagnostics.Debug.WriteLine($"[ExtractUniversalValue] Script component detected: {originalObject.Name}, ShouldIncludeProperty('Script')={shouldInclude}, Text length={scriptDebug.Text?.Length ?? 0}");
            }

#endif
            // Extract value directly from the Grasshopper object based on component type
            return originalObject switch
            {
                // Number Slider: "5<2,10.000>" format (decimal places encoded in the numbers)
                GH_NumberSlider slider when propertyManager.ShouldIncludeProperty("CurrentValue", originalObject) =>
                    FormatSliderValue(slider),

                // Panel: plain text
                GH_Panel panel when propertyManager.ShouldIncludeProperty("UserText", originalObject) =>
                    panel.UserText,

                // Scribble: plain text
                GH_Scribble scribble when propertyManager.ShouldIncludeProperty("Text", originalObject) =>
                    scribble.Text,

                // Value List: array of items
                GH_ValueList valueList when propertyManager.ShouldIncludeProperty("ListItems", originalObject) =>
                    valueList.ListItems.Select(item => new Dictionary<string, object>
                    {
                        ["Name"] = item.Name,
                        ["Expression"] = item.Expression
                    }).ToList(),

                // Script components: extract script code using IScriptComponent.Text
                IScriptComponent scriptComp when propertyManager.ShouldIncludeProperty("Script", originalObject) =>
                    scriptComp.Text,

                _ => null
            };
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

        /// <summary>
        /// Extracts type hint for a parameter from the RunScript method signature in script code.
        /// </summary>
        /// <param name="scriptCode">The script code containing the RunScript method.</param>
        /// <param name="variableName">The parameter variable name to find.</param>
        /// <param name="isInput">True for input parameters, false for output parameters.</param>
        /// <returns>Type hint string (e.g., "List&lt;Curve&gt;", "Interval") or null if not found.</returns>
        private static string ExtractTypeHintFromScriptSignature(string scriptCode, string variableName, bool isInput)
        {
            if (string.IsNullOrEmpty(scriptCode) || string.IsNullOrEmpty(variableName))
                return null;

            try
            {
                // Try C# style: private void RunScript(...)
                var csharpMatch = System.Text.RegularExpressions.Regex.Match(
                    scriptCode,
                    @"private\s+void\s+RunScript\s*\((.*?)\)",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (csharpMatch.Success)
                {
                    return ExtractTypeFromCSharpSignature(csharpMatch.Groups[1].Value, variableName, isInput);
                }

                // Try Python style: def RunScript(self, ...)
                var pythonMatch = System.Text.RegularExpressions.Regex.Match(
                    scriptCode,
                    @"def\s+RunScript\s*\(\s*self\s*,\s*(.*?)\s*\)\s*:",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (pythonMatch.Success)
                {
                    return ExtractTypeFromPythonSignature(pythonMatch.Groups[1].Value, variableName, isInput);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtractTypeHintFromScriptSignature] Error parsing signature: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts type hint from C# RunScript signature parameters.
        /// </summary>
        private static string ExtractTypeFromCSharpSignature(string parametersStr, string variableName, bool isInput)
        {
            // Split by comma, handling nested generics like List<Curve>
            var parameters = SplitParameters(parametersStr);
            
            System.Diagnostics.Debug.WriteLine($"[ExtractTypeFromCSharpSignature] Looking for '{variableName}' (isInput={isInput}) in {parameters.Count} parameters");

            foreach (var param in parameters)
            {
                var trimmed = param.Trim();
                
                // C# format: "Type varName" or "ref Type varName" (outputs use ref)
                var isRef = trimmed.StartsWith("ref ");
                if (isRef != !isInput) // ref params are outputs, non-ref are inputs
                {
                    System.Diagnostics.Debug.WriteLine($"[ExtractTypeFromCSharpSignature] Skipping '{trimmed}' - isRef={isRef}, looking for isInput={isInput}");
                    continue;
                }

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var paramName = parts[parts.Length - 1].Trim();
                    // Handle @out -> out comparison
                    var cleanParamName = paramName.TrimStart('@');
                    var cleanVariableName = variableName.TrimStart('@');
                    
                    System.Diagnostics.Debug.WriteLine($"[ExtractTypeFromCSharpSignature] Comparing '{cleanParamName}' with '{cleanVariableName}'");
                    
                    if (cleanParamName.Equals(cleanVariableName, StringComparison.Ordinal))
                    {
                        // Type is everything except the last part (variable name) and "ref" if present
                        var typeStartIdx = isRef ? 1 : 0;
                        var typeParts = new string[parts.Length - 1 - typeStartIdx];
                        Array.Copy(parts, typeStartIdx, typeParts, 0, typeParts.Length);
                        var typeHint = string.Join(" ", typeParts).Trim();
                        System.Diagnostics.Debug.WriteLine($"[ExtractTypeFromCSharpSignature] ✓ Found type '{typeHint}' for '{variableName}'");
                        return typeHint;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ExtractTypeFromCSharpSignature] ✗ No match found for '{variableName}'");
            return null;
        }

        /// <summary>
        /// Extracts type hint from Python RunScript signature parameters.
        /// </summary>
        private static string ExtractTypeFromPythonSignature(string parametersStr, string variableName, bool isInput)
        {
            // Python doesn't distinguish input/output in signature - all are inputs, outputs are return values
            // For now, only extract input types
            if (!isInput)
                return null;

            var parameters = SplitParameters(parametersStr);

            foreach (var param in parameters)
            {
                var trimmed = param.Trim();
                
                // Python format: "varName" (no type hints in Grasshopper Python scripts typically)
                // Just match by name
                if (trimmed.Equals(variableName, StringComparison.Ordinal))
                {
                    // Python scripts in Grasshopper don't have type hints in signature
                    // Return null - types are inferred at runtime
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Splits parameter string by commas, respecting nested generics like List&lt;Curve&gt;.
        /// </summary>
        private static List<string> SplitParameters(string parametersStr)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            int depth = 0;

            foreach (char c in parametersStr)
            {
                if (c == '<')
                {
                    depth++;
                    current.Append(c);
                }
                else if (c == '>')
                {
                    depth--;
                    current.Append(c);
                }
                else if (c == ',' && depth == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }

        /// <summary>
        /// Extracts type hint from ScriptVariableParam.TypeHints property.
        /// Works for all script languages (C#, Python, VB, IronPython).
        /// </summary>
        private static string InferTypeHintFromParameter(IGH_Param param, IScriptComponent scriptComp)
        {
            System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] Starting for parameter '{param.Name}', Type='{param.GetType().FullName}'");
            
            try
            {
                // ScriptVariableParam stores type info in private _converter field
                // Access it via reflection
                var converterField = param.GetType().GetField("_converter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] _converter field: {(converterField != null ? "FOUND" : "NOT FOUND")}");
                
                if (converterField != null)
                {
                    var converter = converterField.GetValue(param);
                    System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] Converter value: {(converter != null ? converter.GetType().FullName : "NULL")}");
                    
                    if (converter != null)
                    {
                        // Get TargetType property from converter
                        var targetTypeProperty = converter.GetType().GetProperty("TargetType");
                        System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] TargetType property: {(targetTypeProperty != null ? "FOUND" : "NOT FOUND")}");
                        
                        if (targetTypeProperty != null)
                        {
                            var targetType = targetTypeProperty.GetValue(converter);
                            System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] TargetType value: {(targetType != null ? targetType.ToString() : "NULL")}");
                            
                            if (targetType != null)
                            {
                                // Get Type property from ParamType
                                var typeProperty = targetType.GetType().GetProperty("Type");
                                if (typeProperty != null)
                                {
                                    var type = typeProperty.GetValue(targetType) as Type;
                                    if (type != null)
                                    {
                                        var typeName = type.Name;
                                        System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] Type.Name: '{typeName}'");
                                        
                                        // Convert to standard format
                                        var result = ConvertTypeHintToStandardFormat(typeName, param.Access);
                                        System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] ✓ Returning '{result}'");
                                        return result;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] ERROR: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[InferTypeHintFromParameter] Stack: {ex.StackTrace}");
            }
            
            // Fallback: generic type based on access mode
            if (param.Access == GH_ParamAccess.tree)
            {
                return "DataTree<object>";
            }
            else if (param.Access == GH_ParamAccess.list)
            {
                return "List<object>";
            }
            else
            {
                return "object";
            }
        }

        /// <summary>
        /// Converts TypeHint name to standard C# format with proper access wrapper.
        /// </summary>
        private static string ConvertTypeHintToStandardFormat(string typeHintName, GH_ParamAccess access)
        {
            // TypeHint names might be like "Curve", "Point3d", "Interval", etc.
            // We need to wrap them appropriately based on access mode
            
            if (access == GH_ParamAccess.tree)
            {
                return $"DataTree<{typeHintName}>";
            }
            else if (access == GH_ParamAccess.list)
            {
                return $"List<{typeHintName}>";
            }
            else
            {
                return typeHintName;
            }
        }
    }
}
