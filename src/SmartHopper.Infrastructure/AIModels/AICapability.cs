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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Defines the capabilities that an AI model can support.
    /// </summary>
    [Flags]
    public enum AICapability
    {
        None = 0,
        
        // Input capabilities
        TextInput = 1 << 0,
        ImageInput = 1 << 1,
        AudioInput = 1 << 2,
        JsonInput = 1 << 3,
        
        // Output capabilities
        TextOutput = 1 << 4,
        ImageOutput = 1 << 5,
        AudioOutput = 1 << 6,
        JsonOutput = 1 << 7,
        
        // Advanced capabilities
        FunctionCalling = 1 << 8,
        Reasoning = 1 << 9,
        
        // Composite capabilities for default definition
        Text2Text = TextInput | TextOutput,
        ToolChat = Text2Text | FunctionCalling,
        ReasoningChat = Text2Text | Reasoning,
        ToolReasoningChat = Text2Text | Reasoning | FunctionCalling,
        Text2Json = TextInput | JsonOutput,
        Text2Image = TextInput | ImageOutput,
        Text2Speech = TextInput | AudioOutput,
        Speech2Text = AudioInput | TextOutput,
        Image2Text = ImageInput | TextOutput,
    }

    /// <summary>
    /// Extension methods for AICapability.
    /// </summary>
    public static class AICapabilityExtensions
    {
        /// <summary>
        /// Formats AICapability flags for clear logging, showing all individual flags.
        /// </summary>
        /// <param name="capabilities">The capabilities to format.</param>
        /// <returns>A string listing all individual capability flags.</returns>
        public static string ToDetailedString(this AICapability capabilities)
        {
            if (capabilities == AICapability.None)
            {
                return "None";
            }

            var flags = new List<string>();
            
            // Check each individual flag
            if ((capabilities & AICapability.TextInput) == AICapability.TextInput)
            {
                flags.Add("TextInput");
            }
            if ((capabilities & AICapability.TextOutput) == AICapability.TextOutput)
            {
                flags.Add("TextOutput");
            }
            if ((capabilities & AICapability.ImageInput) == AICapability.ImageInput)
            {
                flags.Add("ImageInput");
            }
            if ((capabilities & AICapability.ImageOutput) == AICapability.ImageOutput)
            {
                flags.Add("ImageOutput");
            }
            if ((capabilities & AICapability.AudioInput) == AICapability.AudioInput)
            {
                flags.Add("AudioInput");
            }
            if ((capabilities & AICapability.AudioOutput) == AICapability.AudioOutput)
            {
                flags.Add("AudioOutput");
            }
            if ((capabilities & AICapability.JsonInput) == AICapability.JsonInput)
            {
                flags.Add("JsonInput");
            }
            if ((capabilities & AICapability.JsonOutput) == AICapability.JsonOutput)
            {
                flags.Add("JsonOutput");
            }
            if ((capabilities & AICapability.FunctionCalling) == AICapability.FunctionCalling)
            {
                flags.Add("FunctionCalling");
            }
            if ((capabilities & AICapability.Reasoning) == AICapability.Reasoning)
            {
                flags.Add("Reasoning");
            }

            return flags.Count > 0 ? string.Join(", ", flags) : "Unknown";
        }

        /// <summary>
        /// Checks if the capability has an input capability.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <returns>True if the capability has an input capability.</returns>
        public static bool HasInput(this AICapability capability)
        {
            return (capability & AICapability.TextInput) == AICapability.TextInput ||
                   (capability & AICapability.ImageInput) == AICapability.ImageInput ||
                   (capability & AICapability.AudioInput) == AICapability.AudioInput ||
                   (capability & AICapability.JsonInput) == AICapability.JsonInput;
        }

        /// <summary>
        /// Checks if the capability has an output capability.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <returns>True if the capability has an output capability.</returns>
        public static bool HasOutput(this AICapability capability)
        {
            return (capability & AICapability.TextOutput) == AICapability.TextOutput ||
                   (capability & AICapability.ImageOutput) == AICapability.ImageOutput ||
                   (capability & AICapability.AudioOutput) == AICapability.AudioOutput ||
                   (capability & AICapability.JsonOutput) == AICapability.JsonOutput;
        }

        /// <summary>
        /// Checks if the capability has a specific flag.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <param name="flag">The flag to check.</param>
        /// <returns>True if the capability has the specified flag.</returns>
        public static bool HasFlag(this AICapability capability, AICapability flag)
        {
            return (capability & flag) == flag;
        }
    }
}
