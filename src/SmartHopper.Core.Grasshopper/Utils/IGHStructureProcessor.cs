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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Serialization.DataTreeConverter
    /// </summary>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Serialization.DataTreeConverter. Please update your references.", false)]
    public static class IGHStructureProcessor
    {
        public static Dictionary<string, List<object>> IGHStructureToDictionary(IGH_Structure structure)
        {
            return DataTreeConverter.IGHStructureToDictionary(structure);
        }

        public static Dictionary<string, object> IGHStructureDictionaryTo1DDictionary(Dictionary<string, List<object>> dictionary)
        {
            return DataTreeConverter.IGHStructureDictionaryTo1DDictionary(dictionary);
        }

        public static GH_Structure<T> JObjectToIGHStructure<T>(JToken input, Func<JToken, T> convertFunction)
            where T : IGH_Goo
        {
            return DataTreeConverter.JObjectToIGHStructure(input, convertFunction);
        }
    }
}