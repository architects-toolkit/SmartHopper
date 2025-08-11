/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Messaging
{
    public static class AIUtils
    {
        /// <summary>
        /// Tries to parse a JSON string into a JToken.
        /// </summary>
        /// <param name="strInput">The JSON string to parse.</param>
        /// <param name="jToken">The output JToken if successful.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public static bool TryParseJson(string strInput, out JToken? jToken)
        {
            try
            {
                jToken = JToken.Parse(strInput);
                return true;
            }
            catch (Exception)
            {
                jToken = null;
                return false;
            }
        }
    }
}
