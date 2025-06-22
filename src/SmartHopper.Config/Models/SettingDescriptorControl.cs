using System;

namespace SmartHopper.Config.Models
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
