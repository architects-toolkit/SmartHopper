/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

// Purpose: Encode/decode GH_Structure<IGH_Goo> to/from GH_Structure<GH_String>
// using SafeGooCodec per item, preserving paths and order. Never throws.

using System.Collections.Generic;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.IO
{
    /// <summary>
    /// Encodes and decodes trees of goo items to string trees using SafeGooCodec.
    /// </summary>
    public static class SafeStructureCodec
    {
        /// <summary>
        /// Encodes a goo tree into a string tree using SafeGooCodec per item.
        /// </summary>
        public static GH_Structure<GH_String> EncodeTree(GH_Structure<IGH_Goo> src)
        {
            var dst = new GH_Structure<GH_String>();
            if (src == null)
                return dst;

            foreach (var path in src.Paths)
            {
                var branch = src.get_Branch(path);
                if (branch == null)
                {
                    dst.EnsurePath(path);
                    continue;
                }

                foreach (var item in branch)
                {
                    var goo = item as IGH_Goo; // branch items may be typed as object at compile-time
                    var payload = SafeGooCodec.Encode(goo);
                    dst.Append(new GH_String(payload), path);
                }
            }

            return dst;
        }

        /// <summary>
        /// Decodes a string tree into a goo tree using SafeGooCodec per item. Collects non-fatal warnings.
        /// </summary>
        public static GH_Structure<IGH_Goo> DecodeTree(GH_Structure<GH_String> src, out List<string> warnings)
        {
            warnings = new List<string>();
            var dst = new GH_Structure<IGH_Goo>();
            if (src == null)
                return dst;

            foreach (var path in src.Paths)
            {
                var branch = src.get_Branch(path);
                if (branch == null)
                {
                    dst.EnsurePath(path);
                    continue;
                }

                foreach (var s in branch)
                {
                    var sstr = s as GH_String;
                    var payload = sstr?.Value ?? string.Empty;
                    if (SafeGooCodec.TryDecode(payload, out var goo, out var warn))
                    {
                        if (!string.IsNullOrEmpty(warn)) warnings.Add(warn);
                        dst.Append(goo, path);
                    }
                    else
                    {
                        warnings.Add("Failed to decode item; appended empty GH_String.");
                        dst.Append(new GH_String(string.Empty), path);
                    }
                }
            }

            return dst;
        }
    }
}
