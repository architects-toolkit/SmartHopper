/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Base class for UI control parameters for a setting descriptor.
    /// </summary>
    public abstract class SettingDescriptorControl
    {
        // Marker base class for per-type control parameters
    }

    /// <summary>
    /// Control parameters for int settings: choose between stepper or slider, with min, max, step.
    /// </summary>
    public class NumericSettingDescriptorControl : SettingDescriptorControl
    {
        public bool UseSlider { get; set; } = false;

        public int Min { get; set; } = 0;

        public int Max { get; set; } = 100;

        public int Step { get; set; } = 1;
    }
}
