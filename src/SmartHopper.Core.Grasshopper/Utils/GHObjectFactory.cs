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
using System.Diagnostics;
using Grasshopper;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public static class GHObjectFactory
    {
        public static IGH_ObjectProxy? FindProxy(Guid guid)
        {
            IGH_ObjectProxy objectProxy = Instances.ComponentServer.EmitObjectProxy(guid);

            if (objectProxy != null)
            {
                return objectProxy;
            }

            Debug.WriteLine($"Error: Object not found by guid: {guid} - {objectProxy}");
            return null;
        }

        public static IGH_ObjectProxy? FindProxy(string name)
        {
            IGH_ObjectProxy objectProxy = Instances.ComponentServer.FindObjectByName(name, true, true);

            if (objectProxy != null)
            {
                return objectProxy;
            }

            Debug.WriteLine($"Error: Object not found by name: {name} - {objectProxy}");
            return null;
        }

        public static IGH_ObjectProxy? FindProxy(Guid guid, string name)
        {
            if (guid == Guid.Empty && !string.IsNullOrEmpty(name))
            {
                return FindProxy(name);
            }
            else if (guid != Guid.Empty)
            {
                return FindProxy(guid);
            }

            return null;
        }

        public static IGH_DocumentObject CreateInstance(IGH_ObjectProxy objectProxy)
        {
            IGH_DocumentObject documentObject = objectProxy.CreateInstance();
            documentObject.CreateAttributes(); // Initialize attributes
            return documentObject;
        }
    }
}
