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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Managers.AITools;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Interfaces
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

        Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false);

        string GetModel(Dictionary<string, object> settings, string requestedModel = "");

        /// <summary>
        /// Injects decrypted settings for this provider (called by ProviderManager).
        /// </summary>
        void InitializeSettings(Dictionary<string, object> settings);

        IEnumerable<SettingDescriptor> GetSettingDescriptors();
    }
}
