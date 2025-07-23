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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.AITools;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Context provider that supplies information about installed Grasshopper components to AI queries
    /// </summary>
    public class AvailableComponentsContextProvider : IAIContextProvider
    {
        public string ProviderId => "available-components";

        public Dictionary<string, string> GetContext()
        {
            // Call gh_list_components tool to retrieve available components
            var result = AIToolManager.ExecuteTool("gh_list_components", new JObject(), null)
                .GetAwaiter().GetResult() as JObject;
            var components = new Dictionary<string, string>();
            if (result != null)
            {
                components["count"] = result["count"]?.ToString();
                var names = result["names"]?.ToObject<List<string>>() ?? new List<string>();
                var guids = result["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var mapping = new JObject();
                for (int i = 0; i < Math.Min(names.Count, guids.Count); i++)
                {
                    mapping[names[i]] = guids[i];
                }
                components["list"] = mapping.ToString(Newtonsoft.Json.Formatting.None);
            }
            return components;
        }
    }
}
