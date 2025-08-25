/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Menu.Dialogs.SettingsTabs.Models
{
    /// <summary>
    /// Model for SmartHopper Assistant settings (CanvasButton and related functionality).
    /// </summary>
    public class AssistantSettings
    {
        /// <summary>
        /// Gets or sets the AI provider to use for SmartHopper Assistant.
        /// </summary>
        public string AssistantProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AI model to use for SmartHopper Assistant.
        /// </summary>
        public string AssistantModel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the Canvas button is enabled.
        /// </summary>
        public bool EnableCanvasButton { get; set; } = true;

        /// <summary>
        /// Gets or sets whether AI-generated greetings are enabled in chat.
        /// </summary>
        public bool EnableAIGreeting { get; set; } = false;
    }
}
