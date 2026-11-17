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

using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Non-executable provider metadata embedded in the DLL and covered by its SHA-256.
    /// Metadata is a publisher claim, not an independent proof of trust.
    /// </summary>
    internal sealed class ProviderManifest
    {
        /// <summary>Gets the embedded resource name used by provider projects.</summary>
        internal const string ResourceName = "SmartHopper.ProviderManifest.json";

        /// <summary>Gets the publisher-declared human-readable name.</summary>
        internal string DisplayName { get; private set; } = string.Empty;

        /// <summary>Gets the declared catalogue snapshot count, or null for dynamic catalogues.</summary>
        internal int? ModelCount { get; private set; }

        /// <summary>
        /// Reads bounded embedded JSON through PE metadata, without loading the assembly,
        /// resolving dependencies, constructing providers, or invoking module initializers.
        /// Invalid or absent metadata falls back to the assembly filename in the caller.
        /// </summary>
        /// <param name="assemblyPath">The provider DLL to inspect as data.</param>
        internal static ProviderManifest? Read(string assemblyPath)
        {
            try
            {
                using var stream = File.OpenRead(assemblyPath);
                using var pe = new PEReader(stream);
                if (!pe.HasMetadata || pe.PEHeaders.CorHeader == null)
                {
                    return null;
                }

                var metadata = pe.GetMetadataReader();
                foreach (var handle in metadata.ManifestResources)
                {
                    var resource = metadata.GetManifestResource(handle);
                    if (!resource.Implementation.IsNil || metadata.GetString(resource.Name) != ResourceName)
                    {
                        continue;
                    }

                    var data = pe.GetSectionData(pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetReader();
                    data.Offset = checked((int)resource.Offset);
                    var length = data.ReadInt32();
                    if (length <= 0 || length > 16384 || length > data.RemainingBytes)
                    {
                        return null;
                    }

                    using var json = new JsonTextReader(new StringReader(data.ReadUTF8(length))) { MaxDepth = 8 };
                    var document = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (json.Read() || document["schemaVersion"]?.Type != JTokenType.Integer || (int)document["schemaVersion"]! != 1
                        || document["displayName"]?.Type != JTokenType.String)
                    {
                        return null;
                    }

                    var name = ((string)document["displayName"]!).Trim();
                    var count = document["modelCount"];
                    if (name.Length == 0 || name.Length > 100 || name.Any(char.IsControl)
                        || (count != null && count.Type != JTokenType.Null
                            && (count.Type != JTokenType.Integer || (long)count < 0 || (long)count > 1000000)))
                    {
                        return null;
                    }

                    return new ProviderManifest
                    {
                        DisplayName = name,
                        ModelCount = count?.Type == JTokenType.Integer ? (int)count : null,
                    };
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                || ex is BadImageFormatException || ex is JsonException || ex is ArgumentException || ex is OverflowException)
            {
                return null;
            }

            return null;
        }
    }
}
