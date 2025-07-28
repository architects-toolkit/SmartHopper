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
        
        // Composite capabilities for convenience
        BasicChat = TextInput | TextOutput,
        AdvancedChat = BasicChat | FunctionCalling,
        MultiModal = TextInput | TextOutput | ImageInput,
        TTS = TextInput | AudioOutput,
        STT = AudioInput | TextOutput,
        All = TextInput | ImageInput | AudioInput | TextOutput | ImageOutput | AudioOutput | FunctionCalling | StructuredOutput | Reasoning
    }
}