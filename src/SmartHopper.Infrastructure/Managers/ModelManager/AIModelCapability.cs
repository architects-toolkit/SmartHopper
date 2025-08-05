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
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// Defines the capabilities that an AI model can support.
    /// </summary>
    [Flags]
    public enum AIModelCapability
    {
        None = 0,
        
        // Input capabilities
        TextInput = 1 << 0,
        ImageInput = 1 << 1,
        AudioInput = 1 << 2,
        
        // Output capabilities
        TextOutput = 1 << 3,
        ImageOutput = 1 << 4,
        AudioOutput = 1 << 5,
        
        // Advanced capabilities
        FunctionCalling = 1 << 6,
        StructuredOutput = 1 << 7,
        Reasoning = 1 << 8,
        
        // Composite capabilities for default definition
        BasicChat = TextInput | TextOutput,
        AdvancedChat = BasicChat | FunctionCalling,
        ReasoningChat = AdvancedChat | Reasoning,
        JsonGenerator = TextInput | StructuredOutput,
        ImageGenerator = TextInput | ImageOutput,
        TTS = TextInput | AudioOutput,
        STT = AudioInput | TextOutput,
    }

    /// <summary>
    /// Extension methods for AIModelCapability enum.
    /// </summary>
    public static class AIModelCapabilityExtensions
    {
        /// <summary>
        /// Formats AIModelCapability flags for clear logging, showing all individual flags.
        /// </summary>
        /// <param name="capabilities">The capabilities to format.</param>
        /// <returns>A string listing all individual capability flags.</returns>
        public static string ToDetailedString(this AIModelCapability capabilities)
        {
            if (capabilities == AIModelCapability.None)
                return "None";

            var flags = new List<string>();
            
            // Check each individual flag
            if ((capabilities & AIModelCapability.TextInput) == AIModelCapability.TextInput)
                flags.Add("TextInput");
            if ((capabilities & AIModelCapability.ImageInput) == AIModelCapability.ImageInput)
                flags.Add("ImageInput");
            if ((capabilities & AIModelCapability.AudioInput) == AIModelCapability.AudioInput)
                flags.Add("AudioInput");
            if ((capabilities & AIModelCapability.TextOutput) == AIModelCapability.TextOutput)
                flags.Add("TextOutput");
            if ((capabilities & AIModelCapability.ImageOutput) == AIModelCapability.ImageOutput)
                flags.Add("ImageOutput");
            if ((capabilities & AIModelCapability.AudioOutput) == AIModelCapability.AudioOutput)
                flags.Add("AudioOutput");
            if ((capabilities & AIModelCapability.FunctionCalling) == AIModelCapability.FunctionCalling)
                flags.Add("FunctionCalling");
            if ((capabilities & AIModelCapability.StructuredOutput) == AIModelCapability.StructuredOutput)
                flags.Add("StructuredOutput");
            if ((capabilities & AIModelCapability.Reasoning) == AIModelCapability.Reasoning)
                flags.Add("Reasoning");

            return flags.Count > 0 ? string.Join(", ", flags) : "Unknown";
        }
    }
}