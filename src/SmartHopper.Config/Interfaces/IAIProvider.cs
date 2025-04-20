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
using System.Drawing;

namespace SmartHopper.Config.Interfaces
{
    public interface IAIProvider
    {
        string Name { get; }

        string DefaultModel { get; }

        /// <summary>
        /// Gets the provider's icon. Should return a 16x16 image suitable for display in the UI.
        /// </summary>
        Image Icon { get; }

        /// <summary>
        /// Gets or sets whether this provider is enabled and should be available for use.
        /// This can be used to disable template or experimental providers.
        /// </summary>
        bool IsEnabled { get; }

        IEnumerable<SettingDescriptor> GetSettingDescriptors();

        bool ValidateSettings(Dictionary<string, object> settings);

        Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false);
        
        string GetModel(Dictionary<string, object> settings, string requestedModel = "");
    }

    public interface IAIProviderSettings
    {
        Control CreateSettingsControl();

        Dictionary<string, object> GetSettings();

        void LoadSettings(Dictionary<string, object> settings);

        bool ValidateSettings();
    }
}
