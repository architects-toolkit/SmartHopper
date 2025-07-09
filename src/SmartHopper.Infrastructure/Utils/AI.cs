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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Managers;
using SmartHopper.Infrastructure.Models;
using System.Text.RegularExpressions;

namespace SmartHopper.Infrastructure.Utils
{
    public static class AI
    {

        /// <summary>
        /// Removes any <think>…</think> tags and their contents from a message body.
        /// </summary>
        /// <param name="messageBody">Original text of the message potentially containing think tags.</param>
        /// <returns>Text with think-tagged sections removed.</returns>
        public static string StripThinkTags(string messageBody)
        {
            if (string.IsNullOrEmpty(messageBody)) return messageBody;
            return Regex.Replace(messageBody, "<think>.*?</think>", string.Empty, RegexOptions.Singleline);
        }
    }
}