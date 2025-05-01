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
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public static class GHDocumentUtils
    {
        // public static IGH_DocumentObject GroupObjects(Guid[] guids, string groupName = null)
        // {
        //    GH_Document doc = GHCanvasUtils.GetCurrentCanvas();
        //    GH_Group group = new GH_Group();

        // if (!string.IsNullOrEmpty(groupName))
        //    {
        //        group.NickName = groupName;
        //        group.Colour = Color.FromArgb(255, 100, 100, 100);
        //    }

        // foreach (var guid in guids)
        //    {
        //        IGH_DocumentObject obj = doc.FindObject(guid, true);
        //        if (obj != null)
        //        {
        //            group.AddObject(obj.InstanceGuid);
        //        }
        //    }

        // doc.AddObject(group, false);
        //    return group;
        // }
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
                    Warnings = GHErrors.GetRuntimeErrors(obj, "warning").ToList(),
                    Errors = GHErrors.GetRuntimeErrors(obj, "error").ToList(),
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
                                        ComponentId = component.InstanceGuid,
                                        ParamName = output.Name,
                                    },
                                    To = new Connection
                                    {
                                        ComponentId = recipientGuid,
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
                                    ComponentId = param.InstanceGuid,
                                    ParamName = param.Name,
                                },
                                To = new Connection
                                {
                                    ComponentId = recipientGuid,
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

                // Get component properties
                var propertyValues = GetObjectProperties(obj);

                // Inject the Script property for script components
                if (obj is IScriptComponent scriptComp)
                {
                    propertyValues["Script"] = scriptComp.Text;
                }

                foreach (var prop in propertyValues)
                {
                    componentProps.Properties[prop.Key] = new ComponentProperty
                    {
                        Value = prop.Value,
                        Type = prop.Value?.GetType().Name ?? "null",
                        HumanReadable = prop.Value?.ToString() ?? "null",
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
                    if (!GHPropertyManager.IsPropertyInWhitelist(property.Name))
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
                        var currentValue = Convert.ToDecimal(value);
                        value = NumberSliderUtils.FormatSliderValue(lowerLimit, upperLimit, currentValue);
                    }

                    // 4) If you have child-property names defined (your whitelist dictionary's value list),
                    //    you can drill in here and pull only those sub-props.
                    var childKeys = GHPropertyManager.GetChildProperties(property.Name);
                    if (childKeys != null)
                    {
                        // Here you'd extract child properties if needed
                        propertyValues[property.Name] = new ComponentProperty
                        {
                            Value = value,
                            Type = value?.GetType().Name ?? "null",
                            HumanReadable = value?.ToString() ?? "null",
                        };
                    }
                    else
                    {
                        // Handle PersistentData specially
                        if (property.Name == "PersistentData")
                        {
                            IGH_Structure? dataTree = value as IGH_Structure;
                            Dictionary<string, List<object>> dictionary = IGHStructureProcessor.IGHStructureToDictionary(dataTree);
                            propertyValues[property.Name] = IGHStructureProcessor.IGHStructureDictionaryTo1DDictionary(dictionary);
                        }
                        else
                        {
                            // Regular leaf property
                            propertyValues[property.Name] = new ComponentProperty
                            {
                                Value = value,
                                Type = value?.GetType().Name ?? "null",
                                HumanReadable = value?.ToString() ?? "null",
                            };
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
