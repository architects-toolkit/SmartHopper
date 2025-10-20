/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    /// <summary>
    /// Extracts comprehensive information from Grasshopper objects and converts them to GhJSON format.
    /// Handles component properties, connections, groups, and metadata extraction.
    /// </summary>
    public static class DocumentIntrospection
    {
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
        /// Extracts component and connection information from Grasshopper objects.
        /// </summary>
        /// <param name="objects">The Grasshopper objects to analyze.</param>
        /// <returns>A GrasshopperDocument with components and connections.</returns>
        private static GrasshopperDocument ExtractDocument(IEnumerable<IGH_ActiveObject> objects)
        {
            var document = new GrasshopperDocument
            {
                Components = new List<ComponentProperties>(),
                Connections = new List<ConnectionPairing>(),
            };

            foreach (var obj in objects)
            {
                // Extract component information
                var componentProps = CreateComponentProperties(obj);
                
                // Extract connections from this object
                ExtractConnectionsFromObject(obj, document.Connections);
                
                document.Components.Add(componentProps);
            }

            return document;
        }

        /// <summary>
        /// Creates component properties from a Grasshopper object.
        /// </summary>
        private static ComponentProperties CreateComponentProperties(IGH_ActiveObject obj)
        {
            var componentProps = new ComponentProperties
            {
                Name = obj.Name,
                InstanceGuid = obj.InstanceGuid,
                ComponentGuid = obj.ComponentGuid,
                Properties = new Dictionary<string, ComponentProperty>(),
                Warnings = ErrorAccess.GetRuntimeErrors(obj, "warning").ToList(),
                Errors = ErrorAccess.GetRuntimeErrors(obj, "error").ToList(),
                Pivot = obj.Attributes?.Pivot ?? PointF.Empty,
                Selected = obj.Attributes?.Selected ?? false,
            };

            // Extract all component properties
            ExtractAllProperties(obj, componentProps);

            return componentProps;
        }

        /// <summary>
        /// Extracts connection information from a single Grasshopper object.
        /// </summary>
        private static void ExtractConnectionsFromObject(IGH_ActiveObject obj, List<ConnectionPairing> connections)
        {
            if (obj is IGH_Component component)
            {
                // Process component outputs to avoid duplicate connections
                foreach (var output in component.Params.Output)
                {
                    foreach (var recipient in output.Recipients)
                    {
                        var recipientGuid = recipient.Attributes?.GetTopLevel?.DocObject?.InstanceGuid ?? Guid.Empty;
                        if (recipientGuid != Guid.Empty)
                        {
                            connections.Add(new ConnectionPairing
                            {
                                From = new Connection
                                {
                                    InstanceId = component.InstanceGuid,
                                    ParamName = output.Name,
                                },
                                To = new Connection
                                {
                                    InstanceId = recipientGuid,
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
                    var recipientGuid = recipient.Attributes?.GetTopLevel?.DocObject?.InstanceGuid ?? Guid.Empty;
                    if (recipientGuid != Guid.Empty)
                    {
                        connections.Add(new ConnectionPairing
                        {
                            From = new Connection
                            {
                                InstanceId = param.InstanceGuid,
                                ParamName = param.Name,
                            },
                            To = new Connection
                            {
                                InstanceId = recipientGuid,
                                ParamName = recipient.Name,
                            },
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Extracts all properties from a Grasshopper object.
        /// </summary>
        private static void ExtractAllProperties(IGH_ActiveObject obj, ComponentProperties componentProps)
        {
            // Get properties using the property manager
            var propertyValues = GetObjectProperties(obj);

            // Handle script components specially
            if (obj is IScriptComponent scriptComp && obj is IGH_Component ghComp)
            {
                ExtractScriptComponentProperties(scriptComp, ghComp, propertyValues);
            }

            // Convert to ComponentProperty format
            foreach (var prop in propertyValues)
            {
                componentProps.Properties[prop.Key] = CreateComponentProperty(prop.Value);
            }

            // Extract schema properties
            ExtractSchemaProperties(componentProps, obj);
        }

        /// <summary>
        /// Extracts script-specific properties and parameters.
        /// </summary>
        private static void ExtractScriptComponentProperties(IScriptComponent scriptComp, IGH_Component ghComp, Dictionary<string, object> propertyValues)
        {
            propertyValues["Script"] = scriptComp.Text;
            propertyValues["MarshInputs"] = scriptComp.MarshInputs;
            propertyValues["MarshOutputs"] = scriptComp.MarshOutputs;
            propertyValues["MarshGuids"] = scriptComp.MarshGuids;

            // Extract input parameters
            if (scriptComp.Inputs != null && scriptComp.Inputs.Any())
            {
                var inputParamsArray = new JArray();
                foreach (var input in scriptComp.Inputs)
                {
                    var ghParam = ParameterAccess.GetInputByName(ghComp, input.VariableName);
                    inputParamsArray.Add(new JObject
                    {
                        ["variableName"] = input.VariableName,
                        ["name"] = input.PrettyName,
                        ["description"] = input.Description ?? string.Empty,
                        ["access"] = input.Access.ToString(),
                        ["simplify"] = ghParam?.Simplify ?? false,
                        ["reverse"] = ghParam?.Reverse ?? false,
                        ["dataMapping"] = ghParam?.DataMapping.ToString() ?? "None",
                    });
                }
                propertyValues["ScriptInputs"] = inputParamsArray;
            }

            // Extract output parameters
            if (scriptComp.Outputs != null && scriptComp.Outputs.Any())
            {
                var outputParamsArray = new JArray();
                foreach (var output in scriptComp.Outputs)
                {
                    var ghParam = ParameterAccess.GetOutputByName(ghComp, output.VariableName);
                    outputParamsArray.Add(new JObject
                    {
                        ["variableName"] = output.VariableName,
                        ["name"] = output.PrettyName,
                        ["description"] = output.Description ?? string.Empty,
                        ["access"] = output.Access.ToString(),
                        ["simplify"] = ghParam?.Simplify ?? false,
                        ["reverse"] = ghParam?.Reverse ?? false,
                        ["dataMapping"] = ghParam?.DataMapping.ToString() ?? "None",
                    });
                }
                propertyValues["ScriptOutputs"] = outputParamsArray;
            }
        }

        /// <summary>
        /// Creates a ComponentProperty from a raw value with appropriate type information.
        /// </summary>
        private static ComponentProperty CreateComponentProperty(object value)
        {
            var typeName = value?.GetType().Name ?? "null";
            string humanReadable = null;

            if (value != null)
            {
                var hr = value.ToString();
                var t = value.GetType();
                var fullName = t.FullName;
                var nameOnly = t.Name;

                // Only include human readable if it provides meaningful information
                if (!string.IsNullOrWhiteSpace(hr) &&
                    hr != fullName &&
                    hr != nameOnly &&
                    typeName != "String" &&
                    typeName != "Boolean" &&
                    typeName != "JArray" &&
                    typeName != "JObject")
                {
                    humanReadable = hr;
                }
            }

            return new ComponentProperty
            {
                Value = value,
                Type = typeName,
                HumanReadable = humanReadable,
            };
        }

        /// <summary>
        /// Gets comprehensive details of Grasshopper objects in GhJSON format.
        /// This is the main public entry point for document extraction.
        /// </summary>
        /// <param name="objects">The objects to extract details from.</param>
        /// <param name="includeMetadata">Whether to include document metadata.</param>
        /// <param name="includeGroups">Whether to include group information. Default is true.</param>
        /// <returns>A complete GrasshopperDocument with all requested information.</returns>
        public static GrasshopperDocument GetObjectsDetails(IEnumerable<IGH_ActiveObject> objects, bool includeMetadata, bool includeGroups = true)
        {
            // Extract document structure
            var document = ExtractDocument(objects);

            // Assign component IDs and finalize schema
            FinalizeDocumentSchema(document, objects);

            // Add optional features
            if (includeGroups)
            {
                ExtractGroupInformation(document);
            }

            if (includeMetadata)
            {
                document.SchemaVersion = "1";
                document.Metadata = CreateDocumentMetadata(objects);
            }

            return document;
        }

        /// <summary>
        /// Finalizes the document schema by assigning IDs and completing property extraction.
        /// </summary>
        /// <param name="document">The document to finalize.</param>
        /// <param name="objects">The original objects for reference.</param>
        private static void FinalizeDocumentSchema(GrasshopperDocument document, IEnumerable<IGH_ActiveObject> objects)
        {
            // Assign sequential IDs to components for referencing
            for (int i = 0; i < document.Components.Count; i++)
            {
                document.Components[i].Id = i + 1;
            }
        }

        /// <summary>
        /// Extracts schema properties for a component.
        /// </summary>
        /// <param name="component">The component to enhance.</param>
        /// <param name="originalObject">The original Grasshopper object.</param>
        private static void ExtractSchemaProperties(ComponentProperties component, IGH_ActiveObject originalObject)
        {
            // Extract basic parameters as simple key-value pairs
            component.Params = ExtractBasicParameters(component, originalObject);

            // Extract structured parameter settings
            ExtractParameterSettings(component, originalObject);

            // Extract component state and universal value
            component.ComponentState = ExtractComponentState(component, originalObject);
        }

        /// <summary>
        /// Extracts basic parameters as simple key-value pairs.
        /// </summary>
        /// <param name="component">The component properties.</param>
        /// <param name="originalObject">The original Grasshopper object.</param>
        /// <returns>A dictionary of basic parameters.</returns>
        private static Dictionary<string, object> ExtractBasicParameters(ComponentProperties component, IGH_ActiveObject originalObject)
        {
            var basicParams = new Dictionary<string, object>();

            // Add nickname if different from name
            if (!string.IsNullOrEmpty(originalObject.NickName) && originalObject.NickName != originalObject.Name)
            {
                basicParams["NickName"] = originalObject.NickName;
            }

            // Add specific basic properties based on component type
            if (originalObject is GH_Panel panel && component.Properties.ContainsKey("UserText"))
            {
                basicParams["UserText"] = component.Properties["UserText"].Value;
            }
            else if (originalObject is GH_Scribble scribble && component.Properties.ContainsKey("Text"))
            {
                basicParams["Text"] = component.Properties["Text"].Value;
            }

            return basicParams.Count > 0 ? basicParams : null;
        }

        /// <summary>
        /// Extracts input and output parameter settings.
        /// </summary>
        /// <param name="component">The component properties.</param>
        /// <param name="originalObject">The original Grasshopper object.</param>
        private static void ExtractParameterSettings(ComponentProperties component, IGH_ActiveObject originalObject)
        {
            var inputSettings = new List<ParameterSettings>();
            var outputSettings = new List<ParameterSettings>();

            if (originalObject is IGH_Component ghComponent)
            {
                // Extract input parameter settings
                foreach (var input in ghComponent.Params.Input)
                {
                    var paramSettings = CreateParameterSettings(input);
                    if (paramSettings != null)
                    {
                        inputSettings.Add(paramSettings);
                    }
                }

                // Extract output parameter settings
                foreach (var output in ghComponent.Params.Output)
                {
                    var paramSettings = CreateParameterSettings(output);
                    if (paramSettings != null)
                    {
                        outputSettings.Add(paramSettings);
                    }
                }
            }
            else if (originalObject is IGH_Param param)
            {
                // For parameters, treat as output
                var paramSettings = CreateParameterSettings(param);
                if (paramSettings != null)
                {
                    outputSettings.Add(paramSettings);
                }
            }

            component.InputSettings = inputSettings.Count > 0 ? inputSettings : null;
            component.OutputSettings = outputSettings.Count > 0 ? outputSettings : null;
        }

        /// <summary>
        /// Creates parameter settings from a Grasshopper parameter.
        /// </summary>
        /// <param name="param">The Grasshopper parameter.</param>
        /// <returns>Parameter settings or null if not applicable.</returns>
        private static ParameterSettings CreateParameterSettings(IGH_Param param)
        {
            var settings = new ParameterSettings
            {
                ParameterName = param.Name
            };

            // Data mapping
            if (param.DataMapping != GH_DataMapping.None)
            {
                settings.DataMapping = param.DataMapping.ToString();
            }

            // Expression handling - expressions are not directly accessible through IGH_Param
            // They would need to be extracted through other means if available
            // TODO: Implement expression extraction if needed

            // Additional settings
            var additionalSettings = new AdditionalParameterSettings();
            bool hasAdditionalSettings = false;

            if (param.Reverse)
            {
                additionalSettings.Reverse = true;
                hasAdditionalSettings = true;
            }

            if (param.Simplify)
            {
                additionalSettings.Simplify = true;
                hasAdditionalSettings = true;
            }

            if (param.Locked)
            {
                additionalSettings.Locked = true;
                hasAdditionalSettings = true;
            }

            // Note: Invert is for boolean data inversion, not list reversal
            // Grasshopper's Reverse property is for list order reversal
            // Invert functionality should be implemented separately for boolean data

            if (hasAdditionalSettings)
            {
                settings.AdditionalSettings = additionalSettings;
            }

            return settings;
        }

        /// <summary>
        /// Extracts component-specific state information.
        /// </summary>
        /// <param name="component">The component properties.</param>
        /// <param name="originalObject">The original Grasshopper object.</param>
        /// <returns>Component state or null if not applicable.</returns>
        private static ComponentState ExtractComponentState(ComponentProperties component, IGH_ActiveObject originalObject)
        {
            var state = new ComponentState();
            bool hasState = false;

            // Extract component-level properties
            if (originalObject is GH_Component ghComponent)
            {
                state.Locked = ghComponent.Locked;
                state.Hidden = ghComponent.Hidden;
                hasState = true;
            }

            // Handle different component types with value consolidation
            if (originalObject is GH_NumberSlider slider && component.Properties.ContainsKey("CurrentValue"))
            {
                // Use universal value property (currentValue format: "5.0<0.0,10.0>")
                state.Value = component.Properties["CurrentValue"].Value?.ToString();
                hasState = true;
            }
            else if (originalObject is GH_Panel panel && component.Properties.ContainsKey("UserText"))
            {
                // Panel: userText maps to value
                state.Value = component.Properties["UserText"].Value?.ToString();
                hasState = true;
            }
            else if (originalObject is GH_Scribble scribble && component.Properties.ContainsKey("Text"))
            {
                // Scribble: text maps to value
                state.Value = component.Properties["Text"].Value?.ToString();
                
                // Keep font and corners for UI formatting
                if (component.Properties.ContainsKey("Font"))
                {
                    state.Font = component.Properties["Font"].Value as Dictionary<string, object>;
                }
                if (component.Properties.ContainsKey("Corners"))
                {
                    state.Corners = component.Properties["Corners"].Value as List<Dictionary<string, float>>;
                }
                hasState = true;
            }
            else if (originalObject is IScriptComponent scriptComp)
            {
                // Script: script maps to value
                if (component.Properties.ContainsKey("Script"))
                {
                    state.Value = component.Properties["Script"].Value?.ToString();
                    hasState = true;
                }
                if (component.Properties.ContainsKey("MarshInputs"))
                {
                    state.MarshInputs = (bool?)component.Properties["MarshInputs"].Value;
                    hasState = true;
                }
                if (component.Properties.ContainsKey("MarshOutputs"))
                {
                    state.MarshOutputs = (bool?)component.Properties["MarshOutputs"].Value;
                    hasState = true;
                }
            }

            // Handle value list components
            if (component.Properties.ContainsKey("ListItems"))
            {
                // Value List: listItems maps to value
                state.Value = component.Properties["ListItems"].Value;
                hasState = true;
            }
            if (component.Properties.ContainsKey("ListMode"))
            {
                state.ListMode = component.Properties["ListMode"].Value?.ToString();
                hasState = true;
            }

            return hasState ? state : null;
        }

        /// <summary>
        /// Creates document metadata by analyzing the components.
        /// </summary>
        /// <param name="objects">The objects to analyze for metadata.</param>
        /// <returns>A DocumentMetadata object with populated fields.</returns>
        private static DocumentMetadata CreateDocumentMetadata(IEnumerable<IGH_ActiveObject> objects)
        {
            var metadata = new DocumentMetadata
            {
                Created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Modified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Author = "SmartHopper AI", // TODO: settings to allow custom author
            };

            // Get Rhino version
            try
            {
                var rhinoVersion = global::Rhino.RhinoApp.Version;
                metadata.RhinoVersion = $"{rhinoVersion.Major}.{rhinoVersion.Minor}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Rhino version: {ex.Message}");
            }

            // Get Grasshopper version
            try
            {
                var ghVersion = global::Grasshopper.Versioning.Version;
                metadata.GrasshopperVersion = ghVersion.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Grasshopper version: {ex.Message}");
            }

            // Scan for plugin dependencies
            var dependencies = new HashSet<string>();
            foreach (var obj in objects)
            {
                try
                {
                    var assembly = obj.GetType().Assembly;
                    var assemblyName = assembly.GetName().Name;

                    // Skip standard Grasshopper and Rhino assemblies
                    if (assemblyName != null &&
                        !assemblyName.StartsWith("Grasshopper") &&
                        !assemblyName.StartsWith("Rhino") &&
                        !assemblyName.StartsWith("System") &&
                        !assemblyName.StartsWith("Microsoft") &&
                        !assemblyName.StartsWith("SmartHopper"))
                    {
                        var version = assembly.GetName().Version;
                        dependencies.Add($"{assemblyName} {version.Major}.{version.Minor}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scanning dependencies: {ex.Message}");
                }
            }

            if (dependencies.Count > 0)
            {
                metadata.Dependencies = dependencies.OrderBy(d => d).ToList();
            }

            return metadata;
        }

        /// <summary>
        /// Extracts group information from the current canvas and populates the document.
        /// </summary>
        /// <param name="document">The document to populate with group information.</param>
        private static void ExtractGroupInformation(GrasshopperDocument document)
        {
            try
            {
                var ghDoc = CanvasAccess.GetCurrentCanvas();
                if (ghDoc == null) return;

                var groups = new List<GroupInfo>();

                // Find all groups in the document
                foreach (var obj in ghDoc.Objects)
                {
                    if (obj is GH_Group group)
                    {
                        var groupInfo = new GroupInfo
                        {
                            InstanceGuid = group.InstanceGuid,
                            Name = group.NickName,
                            Members = new List<Guid>(),
                        };

                        // Extract color in ARGB format
                        if (group.Colour != System.Drawing.Color.Empty)
                        {
                            groupInfo.Color = $"{group.Colour.A},{group.Colour.R},{group.Colour.G},{group.Colour.B}";
                        }

                        // Get all members of this group
                        foreach (var memberObj in group.Objects())
                        {
                            groupInfo.Members.Add(memberObj.InstanceGuid);
                        }

                        groups.Add(groupInfo);
                    }
                }

                // Populate document groups
                if (groups.Count > 0)
                {
                    document.Groups = groups;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting group information: {ex.Message}");
            }
        }

        public static Dictionary<string, object> GetObjectProperties(IGH_DocumentObject obj)
        {
            Type type = obj.Attributes.DocObject.GetType();
            PropertyInfo[] properties = type.GetProperties();
            Dictionary<string, object> propertyValues = new ();

            foreach (PropertyInfo property in properties)
            {
                try
                {
                    // 1) Never serialize the GH runtime Params collection (it loops back on itself)
                    if (property.Name == "Params")
                    {
                        continue;
                    }

                    // 2) Only include properties you've explicitly whitelisted
                    if (!PropertyManager.IsPropertyInWhitelist(property.Name))
                    {
                        continue;
                    }

                    // 3) Now get the value and build your ComponentProperty
                    var value = property.GetValue(obj) ?? string.Empty;

                    // Special handling for number slider current value
                    if (obj is GH_NumberSlider slider && property.Name == "CurrentValue")
                    {
                        var instanceDesc = slider.InstanceDescription;
                        var (accuracy, lowerLimit, upperLimit) = NumberSliderUtils.ParseInstanceDescription(instanceDesc);
                        var currentValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                        value = NumberSliderUtils.FormatSliderValue(lowerLimit, upperLimit, currentValue);
                    }

                    // 4) If you have child-property names defined (your whitelist dictionary's value list),
                    //    you can drill in here and pull only those sub-props.
                    var childKeys = PropertyManager.GetChildProperties(property.Name);
                    if (childKeys != null)
                    {
                        // Here you'd extract child properties if needed
                        propertyValues[property.Name] = value;
                    }
                    else
                    {
                        // Handle PersistentData specially
                        if (property.Name == "PersistentData")
                        {
                            IGH_Structure? dataTree = value as IGH_Structure;
                            Dictionary<string, List<object>> dictionary = DataTreeConverter.IGHStructureToDictionary(dataTree);
                            propertyValues[property.Name] = DataTreeConverter.IGHStructureDictionaryTo1DDictionary(dictionary);
                        }
                        else
                        {
                            // Regular leaf property
                            propertyValues[property.Name] = value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting property {property.Name} from {obj}: {ex.Message}");
                }
            }

            return propertyValues;
        }
    }
}
