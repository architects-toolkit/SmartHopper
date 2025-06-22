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
using Newtonsoft.Json.Linq;

namespace SmartHopper.Config.Models
{
    /// <summary>
    /// Carries context and state through the provider call phases.
    /// </summary>
    public sealed class RequestContext
    {
        // Input parameters
        public string               Model                   { get; set; }
        public JArray               Messages                { get; set; }
        public string               JsonSchema              { get; set; }
        public string               Endpoint                { get; set; }
        public bool                 IncludeToolDefinitions  { get; set; }
        public bool                 DoStreaming             { get; set; }
        public IProgress<ChatChunk>? Progress                { get; set; }

        // Shared request body
        public JObject              Body                    { get; set; }

        // Results
        public string?              RawJson                 { get; set; }
        public string?              AccumulatedText         { get; set; }
        public AIResponse?          Response                { get; set; }
    }
}
