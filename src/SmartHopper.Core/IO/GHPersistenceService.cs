/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

// Purpose: Grasshopper-specific implementation of a safe, versioned persistence service.
// Stores output trees as GH_Structure<GH_String> (encoded payloads) per output parameter GUID.
// Reading never throws; decoding warnings are collected for the caller to surface.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.IO
{
    /// <summary>
    /// Implements safe v2 persistence using canonical string trees.
    /// </summary>
    public sealed class GHPersistenceService : IPersistenceService
    {
        /// <inheritdoc />
        public bool WriteOutputsV2(GH_IWriter writer, IGH_Component component, IDictionary<Guid, GH_Structure<IGH_Goo>> outputsByGuid)
        {
            try
            {
                writer.SetInt32(PersistenceConstants.VersionKey, PersistenceConstants.CurrentVersion);

                if (outputsByGuid == null)
                    return true;

                foreach (var kvp in outputsByGuid)
                {
                    var key = PersistenceConstants.KeyForOutputV2(kvp.Key);
                    var tree = kvp.Value ?? new GH_Structure<IGH_Goo>();

                    // Encode goo tree to string tree
                    var encoded = SafeStructureCodec.EncodeTree(tree);

                    // Write into a loose chunk to preserve GH format
                    var chunk = new GH_LooseChunk(key);
                    encoded.Write(chunk);
                    var bytes = chunk.Serialize_Binary();
                    writer.SetByteArray(key, bytes);

#if DEBUG
                    Debug.WriteLine($"[Persistence] Wrote V2 output for {component.Name} key={key}, paths={encoded.PathCount}, items={encoded.DataCount}");
#endif
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Persistence] Exception in WriteOutputsV2: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc />
        public IDictionary<Guid, GH_Structure<IGH_Goo>> ReadOutputsV2(GH_IReader reader, IGH_Component component)
        {
            var result = new Dictionary<Guid, GH_Structure<IGH_Goo>>();
            try
            {
                var version = -1;
                try
                {
                    if (reader.ItemExists(PersistenceConstants.VersionKey))
                    {
                        version = reader.GetInt32(PersistenceConstants.VersionKey);
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine($"[Persistence] Exception reading version key: {ex.Message}");
#endif
                }

                if (version != PersistenceConstants.CurrentVersion)
                {
#if DEBUG
                    Debug.WriteLine($"[Persistence] Version mismatch or missing (found={version}); skipping V2 restore for {component.Name}");
#endif
                    return result;
                }

                // Iterate component outputs and try read each by GUID
                foreach (var p in component.Params.Output)
                {
                    var guid = p.InstanceGuid;
                    var key = PersistenceConstants.KeyForOutputV2(guid);

                    try
                    {
                        if (!reader.ItemExists(key))
                            continue;

                        var bytes = reader.GetByteArray(key);
                        if (bytes == null || bytes.Length == 0)
                            continue;

                        var c = new GH_LooseChunk(key);
                        c.Deserialize_Binary(bytes);

                        var encoded = new GH_Structure<GH_String>();
                        encoded.Read(c);

                        var decoded = SafeStructureCodec.DecodeTree(encoded, out var warnings);
                        if (warnings != null && warnings.Count > 0)
                        {
#if DEBUG
                            foreach (var w in warnings)
                                Debug.WriteLine($"[Persistence] Decode warning for key={key}: {w}");
#endif
                        }

                        result[guid] = decoded;
                    }
                    catch (Exception ex)
                    {
                        // Never throw; skip this param
                        Debug.WriteLine($"[Persistence] Exception reading key={key}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Persistence] Exception in ReadOutputsV2: {ex.Message}");
            }

            return result;
        }
    }
}
