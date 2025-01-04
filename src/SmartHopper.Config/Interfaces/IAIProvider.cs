/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;
using SmartHopper.Config.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartHopper.Config.Interfaces
{
    public interface IAIProvider
    {
        string Name { get; }

        IEnumerable<SettingDescriptor> GetSettingDescriptors();

        bool ValidateSettings(Dictionary<string, object> settings);

        Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "");
    }

    public interface IAIProviderSettings
    {
        Control CreateSettingsControl();

        Dictionary<string, object> GetSettings();

        void LoadSettings(Dictionary<string, object> settings);

        bool ValidateSettings();
    }
}
