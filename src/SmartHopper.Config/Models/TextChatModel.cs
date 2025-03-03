/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/agreentejada/winforms-chat
 * MIT License
 * Copyright (c) 2020 agreentejada
 */

using System;
using System.Collections.Generic;

namespace SmartHopper.Config.Models
{
    public interface IChatModel
    {
        bool Inbound { get; set; }
        bool Read { get; set; }
        DateTime Time { get; set; }
        string Author { get; set; }
        string Type { get; }
        string ToolName { get; set; }
        string ToolArgs { get; set; }
        string ToolCallId { get; set; }
    }

    public class TextChatModel : IChatModel
    {
        public bool Inbound { get; set; }
        public bool Read { get; set; }
        public DateTime Time { get; set; }
        public string Author { get; set; }
        public string Type { get; set; } = "text";
        public string Body { get; set; }
        public string ToolName { get; set; } = "";
        public string ToolArgs { get; set; } = "";
        public string ToolCallId { get; set; } = "";
    }

    public class ToolChatModel : IChatModel
    {
        public bool Inbound { get; set; }
        public bool Read { get; set; }
        public DateTime Time { get; set; }
        public string Author { get; set; }
        public string Type { get; set; } = "text";
        public string Body { get; set; }
        public string ToolName { get; set; } = "";
        public string ToolArgs { get; set; } = "";
        public string ToolCallId { get; set; } = "";
        public List<KeyValuePair<string, string>> ToolResult { get; set; }
    }
}
