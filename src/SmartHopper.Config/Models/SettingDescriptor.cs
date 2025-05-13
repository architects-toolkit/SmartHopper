/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Config.Models
{
    public class SettingDescriptor
    {
        public string Name { get; set; }
        public Type Type { get; set; } = typeof(string);
        public object DefaultValue { get; set; }
        public bool IsSecret { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }
}
