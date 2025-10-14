/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Grasshopper.Utils.Parsing;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Helper methods for parsing AI responses into specific data types.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Parsing.AIResponseParser</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Parsing.AIResponseParser. Please update your references.", false)]
    public static partial class ParsingTools
    {
        public static bool? ParseBooleanFromResponse(string response)
        {
            return AIResponseParser.ParseBooleanFromResponse(response);
        }

        public static List<int> ParseIndicesFromResponse(string response)
        {
            return AIResponseParser.ParseIndicesFromResponse(response);
        }

        public static string ConcatenateItemsToJson(List<GH_String> inputList, string output = "dict")
        {
            return AIResponseParser.ConcatenateItemsToJson(inputList, output);
        }

        public static List<string> ParseStringArrayFromResponse(string response)
        {
            return AIResponseParser.ParseStringArrayFromResponse(response);
        }

        public static string NormalizeJsonArrayString(List<string> values)
        {
            return AIResponseParser.NormalizeJsonArrayString(values);
        }
    }
}