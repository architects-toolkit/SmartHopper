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

            if (!string.IsNullOrEmpty(groupName))
            {
                group.NickName = groupName;
                group.Colour = color ?? System.Drawing.Color.FromArgb(255, 0, 200, 0);
            }

            // Add objects to group
            foreach (var guid in guids)
            {
                var obj = CanvasAccess.FindInstance(guid);
                if (obj != null)
                {
                    group.AddObject(guid);
                }
            }

            // Record undo event before adding the group
            group.RecordUndoEvent("[SH] Group");

            // Add the group to the document with undo support enabled
            doc.AddObject(group, true);

            return group;
        }

        public static GrasshopperDocument GetObjectsDetails(IEnumerable<IGH_ActiveObject> objects)
        {
            var document = new GrasshopperDocument
            {
                Components = new List<ComponentProperties>(),
                Connections = new List<ConnectionPairing>(),
            };

            foreach (var obj in objects)
            {
                var componentProps = new ComponentProperties
                {
                    Name = obj.Name,
                    InstanceGuid = obj.InstanceGuid,
                    ComponentGuid = obj.ComponentGuid,
                    Properties = new Dictionary<string, ComponentProperty>(),
                    Warnings = ErrorAccess.GetRuntimeErrors(obj, "warning").ToList(),
                    Errors = ErrorAccess.GetRuntimeErrors(obj, "error").ToList(),
                };

                if (obj is IGH_Component component)
                {
                    componentProps.Type = "IGH_Component";
                    componentProps.ObjectType = component.GetType().ToString();

                    // Only process outputs to avoid duplicate connections
                    foreach (var output in component.Params.Output)
                    {
                        foreach (var recipient in output.Recipients)
                        {
                            var recipientGuid = recipient.Attributes?.GetTopLevel?.DocObject?.InstanceGuid ?? Guid.Empty;
                            if (recipientGuid != Guid.Empty)
                            {
                                var connection = new ConnectionPairing
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
                                };
                                document.Connections.Add(connection);
                            }
                        }
                    }
                }
                else if (obj is IGH_Param param)
                {
                    componentProps.Type = "IGH_Param";
                    componentProps.ObjectType = param.GetType().ToString();

                    // Only process outputs to avoid duplicate connections
                    foreach (var recipient in param.Recipients)
                    {
                        var recipientGuid = recipient.Attributes?.GetTopLevel?.DocObject?.InstanceGuid ?? Guid.Empty;
                        if (recipientGuid != Guid.Empty)
                        {
                            var connection = new ConnectionPairing
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
                            };
                            document.Connections.Add(connection);
                        }
                    }
                }
                else
                {
                    componentProps.Type = "other";
                    componentProps.ObjectType = obj.GetType().ToString();
                }

                // ComponentRetriever component properties
                var propertyValues = GetObjectProperties(obj);

                // Inject script properties for script components
                if (obj is IScriptComponent scriptComp && obj is IGH_Component ghComp)
                {
                    // Add the script text content
                    propertyValues["Script"] = scriptComp.Text;

                    // Add information about language and marshaling options
                    //if (scriptComp.LanguageSpec != null)
                    //{
                    //    propertyValues["ScriptLanguage"] = scriptComp.LanguageSpec.Name;
                    //}
                    propertyValues["MarshInputs"] = scriptComp.MarshInputs;
                    propertyValues["MarshOutputs"] = scriptComp.MarshOutputs;
                    propertyValues["MarshGuids"] = scriptComp.MarshGuids;

                    // Serialize input parameters
                    if (scriptComp.Inputs != null && scriptComp.Inputs.Any())
                    {
                        var inputParamsArray = new JArray();
                        foreach (var input in scriptComp.Inputs)
                        {
                            // find the real GH_Param
                            var ghParam = ParameterAccess.GetInputByName(ghComp, input.VariableName);

                            var paramObj = new JObject
                            {
                                ["variableName"] = input.VariableName,
                                ["name"] = input.PrettyName,
                                ["description"] = input.Description ?? string.Empty,
                                ["access"] = input.Access.ToString(),

                                // pull the modifiers from the GH_Param, not from IScriptParameter
                                ["simplify"] = ghParam?.Simplify ?? false,
                                ["reverse"] = ghParam?.Reverse ?? false,
                                ["dataMapping"] = ghParam?.DataMapping.ToString() ?? "None",
                            };
                            inputParamsArray.Add(paramObj);
                        }

                        propertyValues["ScriptInputs"] = inputParamsArray;
                    }

                    // Serialize output parameters
                    if (scriptComp.Outputs != null && scriptComp.Outputs.Any())
                    {
                        var outputParamsArray = new JArray();
                        foreach (var output in scriptComp.Outputs)
                        {
                            // find the real GH_Param
                            var ghParam = ParameterAccess.GetOutputByName(ghComp, output.VariableName);

                            var paramObj = new JObject
                            {
                                ["variableName"] = output.VariableName,
                                ["name"] = output.PrettyName,
                                ["description"] = output.Description ?? string.Empty,
                                ["access"] = output.Access.ToString(),

                                // pull the modifiers from the GH_Param, not from IScriptParameter
                                ["simplify"] = ghParam?.Simplify ?? false,
                                ["reverse"] = ghParam?.Reverse ?? false,
                                ["dataMapping"] = ghParam?.DataMapping.ToString() ?? "None",
                            };
                            outputParamsArray.Add(paramObj);
                        }

                        propertyValues["ScriptOutputs"] = outputParamsArray;
                    }
                }

                // Only set humanReadable for non-primitive types when ToString() is meaningful
                foreach (var prop in propertyValues)
                {
                    var val = prop.Value;
                    var typeName = val?.GetType().Name ?? "null";
                    string humanReadable = null;
                    if (val != null)
                    {
                        var hr = val.ToString();
                        var t = val.GetType();
                        var fullName = t.FullName;
                        var nameOnly = t.Name;

                        if (!string.IsNullOrWhiteSpace(hr)
                            && hr != fullName
                            && hr != nameOnly
                            && typeName != "String"
                            && typeName != "Boolean"
                            && typeName != "JArray"
                            && typeName != "JObject")
                        {
                            humanReadable = hr;
                        }
                    }

                    componentProps.Properties[prop.Key] = new ComponentProperty
                    {
                        Value = val,
                        Type = typeName,
                        HumanReadable = humanReadable,
                    };
                }

                // Get component position and selection state
                if (obj.Attributes != null)
                {
                    componentProps.Pivot = obj.Attributes.Pivot;
                    componentProps.Selected = obj.Attributes.Selected;
                }

                document.Components.Add(componentProps);
            }

            return document;
        }

        /// <summary>
        /// Gets details of Grasshopper objects with optional metadata.
        /// </summary>
        /// <param name="objects">The objects to extract details from.</param>
        /// <param name="includeMetadata">Whether to include document metadata.</param>
        /// <returns>A GrasshopperDocument with component and connection details.</returns>
        public static GrasshopperDocument GetObjectsDetails(IEnumerable<IGH_ActiveObject> objects, bool includeMetadata)
        {
            var document = GetObjectsDetails(objects);

            if (includeMetadata)
            {
                document.SchemaVersion = "1"; // TODO: constant to specify version
                document.Metadata = CreateDocumentMetadata(objects);
            }

            return document;
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
