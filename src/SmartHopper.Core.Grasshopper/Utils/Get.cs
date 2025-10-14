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
using SmartHopper.Core.Grasshopper.Utils.Internal;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Internal.ComponentRetriever</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Internal.ComponentRetriever. Please update your references.", false)]
    internal sealed class Get
    {
        public static readonly Dictionary<string, string> FilterSynonyms = ComponentRetriever.FilterSynonyms;
        public static readonly Dictionary<string, string> TypeSynonyms = ComponentRetriever.TypeSynonyms;
        public static readonly Dictionary<string, string> CategorySynonyms = ComponentRetriever.CategorySynonyms;

        public static (HashSet<string> Include, HashSet<string> Exclude) ParseIncludeExclude(IEnumerable<string> rawGroups, Dictionary<string, string> synonyms)
        {
            return ComponentRetriever.ParseIncludeExclude(rawGroups, synonyms);
        }
    }
}