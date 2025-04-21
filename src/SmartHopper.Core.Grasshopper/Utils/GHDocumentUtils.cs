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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.JSON;
using System;
using System.Collections.Generic;
#if WINDOWS
using System.Drawing;
#else
using Eto.Drawing;
#endif
using System.Linq;
using System.Reflection;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public class GHDocumentUtils
    {
        //public static IGH_DocumentObject GroupObjects(Guid[] guids, string groupName = null)
        //{
        //    GH_Document doc = GHCanvasUtils.GetCurrentCanvas();
        //    GH_Group group = new GH_Group();

        //    if (!string.IsNullOrEmpty(groupName))
        //    {
        //        group.NickName = groupName;
        //        group.Colour = Color.FromArgb(255, 100, 100, 100);
        //    }

        //    foreach (var guid in guids)
        //    {
        //        IGH_DocumentObject obj = doc.FindObject(guid, true);
        //        if (obj != null)
        //        {
        //            group.AddObject(obj.InstanceGuid);
        //        }
        //    }

        //    doc.AddObject(group, false);
        //    return group;
        //}

        public static GrasshopperDocument GetObjectsDetails(IEnumerable<IGH_ActiveObject> objects)
        {
            var document = new GrasshopperDocument
            {
                Components = new List<ComponentProperties>(),
                Connections = new List<ConnectionPairing>()
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
                    Errors = GHErrors.GetRuntimeErrors(obj, "error").ToList()
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
                                        ParamName = output.Name
                                    },
                                    To = new Connection
                                    {
                                        ComponentId = recipientGuid,
                                        ParamName = recipient.Name
                                    }
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
                                    ParamName = param.Name
                                },
                                To = new Connection
                                {
                                    ComponentId = recipientGuid,
                                    ParamName = recipient.Name
                                }
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
                foreach (var prop in propertyValues)
                {
                    componentProps.Properties[prop.Key] = new ComponentProperty
                    {
                        Value = prop.Value,
                        Type = prop.Value?.GetType().Name ?? "null",
                        HumanReadable = prop.Value?.ToString() ?? "null"
                    };
                }

                // Get component position and selection state
                if (obj.Attributes != null)
                {
#if WINDOWS
                    componentProps.Pivot = obj.Attributes.Pivot;
#else
                    var p = obj.Attributes.Pivot;
                    componentProps.Pivot = new Eto.Drawing.PointF(p.X, p.Y);
#endif
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
            Dictionary<string, object> propertyValues = new Dictionary<string, object>();

            foreach (PropertyInfo property in properties)
            {
                try
                {
                    if (!JsonProperties.IsPropertyInWhitelist(property.Name))
                    {
                        continue;
                    }

                    var value = property.GetValue(obj) ?? "";

                    // Special handling for number slider current value
                    if (obj is GH_NumberSlider slider && property.Name == "CurrentValue")
                    {
                        var instanceDesc = slider.InstanceDescription;
                        var (accuracy, lowerLimit, upperLimit) = NumberSliderUtils.ParseInstanceDescription(instanceDesc);
                        var currentValue = Convert.ToDecimal(value);
                        value = NumberSliderUtils.FormatSliderValue(lowerLimit, upperLimit, currentValue);
                    }

                    // Check if the property has child properties
                    if (JsonProperties.HasChildProperties(property.Name))
                    {
                        var childPropertyValues = JsonProperties.GetChildProperties(value, property.Name);
                        propertyValues[property.Name] = new ComponentProperty
                        {
                            Value = value,
                            Type = value?.GetType().Name ?? "null",
                            HumanReadable = value?.ToString() ?? "null"
                        };
                    }
                    else
                    {
                        if (new[] { "PersistentData" }.Contains(property.Name))
                        {
                            IGH_Structure dataTree = value as IGH_Structure;
                            Dictionary<string, List<object>> dictionary = IGHStructureProcessor.IGHStructureToDictionary(dataTree);
                            propertyValues[property.Name] = IGHStructureProcessor.IGHStructureDictionaryTo1DDictionary(dictionary);
                        }
                        else
                        {
                            propertyValues[property.Name] = new ComponentProperty
                            {
                                Value = value,
                                Type = value?.GetType().Name ?? "null",
                                HumanReadable = value?.ToString() ?? "null"
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
