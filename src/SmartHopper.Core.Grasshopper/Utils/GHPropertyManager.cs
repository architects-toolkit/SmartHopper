/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Manages component property access and modification.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyManager</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyManager. Please update your references.", false)]
    public static class GHPropertyManager
    {
        public static bool IsPropertyInWhitelist(string propertyName)
        {
            return PropertyManager.IsPropertyInWhitelist(propertyName);
        }

        public static List<string>? GetChildProperties(string propertyName)
        {
            return PropertyManager.GetChildProperties(propertyName);
        }

        public static void SetProperties(object instance, Dictionary<string, ComponentProperty> properties)
        {
            PropertyManager.SetProperties(instance, properties);
        }

        public static void SetProperty(object obj, string propertyPath, object value)
        {
            PropertyManager.SetProperty(obj, propertyPath, value);
        }

        public static bool IsPropertyOmitted(string propertyName)
        {
            return PropertyManager.IsPropertyOmitted(propertyName);
        }

        public static void SetPersistentData(IGH_DocumentObject instance, JObject persistentDataDict)
        {
            PropertyManager.SetPersistentData(instance, persistentDataDict);
        }
    }
}