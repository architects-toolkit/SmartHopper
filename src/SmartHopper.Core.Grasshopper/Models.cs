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

namespace SmartHopper.Core.Grasshopper
{
    public class ComponentProperties
    {
        public string Name { get; set; }
        public Guid InstanceGuid { get; set; }
        public Guid ComponentGuid { get; set; }
        public string Type { get; set; }
        public string ObjectType { get; set; }
        public Dictionary<string, ComponentProperty> Properties { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }
        public PointF Pivot { get; set; }
        public bool Selected { get; set; }
    }

    public class ComponentProperty
    {
        public object Value { get; set; }
        public string Type { get; set; }
        public string HumanReadable { get; set; }
    }

    public class Connection
    {
        public Guid ComponentId { get; set; }
        public string ParamName { get; set; }
    }

    public class ConnectionPairing
    {
        public Connection From { get; set; }
        public Connection To { get; set; }
    }

    public class GrasshopperDocument
    {
        public List<ComponentProperties> Components { get; set; }
        public List<ConnectionPairing> Connections { get; set; }
    }
}
