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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartHopper.Infrastructure.FileSystem
{
    /// <summary>Stores user-approved local file and folder grants with their capabilities.</summary>
    public sealed class LocalFileAllowlist
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new StringEnumConverter() },
        };

        private readonly string path;

        /// <summary>Gets the desktop user's persistent file permissions.</summary>
        public static LocalFileAllowlist Current { get; } = new LocalFileAllowlist(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper", "SmartHopper", "local-file-permissions.json"));

        /// <summary>Initializes a permission store at an explicit path, also used by isolated tests.</summary>
        public LocalFileAllowlist(string path)
        {
            this.path = Path.GetFullPath(path);
        }

        /// <summary>Reads current grants on every lookup so revocations are not hidden by a cache.</summary>
        public IReadOnlyList<LocalFileGrant> GetGrants()
        {
            if (!File.Exists(this.path))
            {
                return Array.Empty<LocalFileGrant>();
            }

            var grants = JsonConvert.DeserializeObject<List<LocalFileGrant>>(File.ReadAllText(this.path))
                ?? throw new InvalidDataException("The local-file permission file is invalid.");
            return grants
                .Where(grant => !string.IsNullOrEmpty(grant.Path) && grant.Capabilities != LocalFileCapability.None)
                .GroupBy(grant => grant.Scope + "|" + grant.Path, LocalPath.Comparer)
                .Select(group => new LocalFileGrant
                {
                    Scope = group.First().Scope,
                    Path = group.First().Path,
                    Capabilities = group.Aggregate(LocalFileCapability.None, (caps, grant) => caps | grant.Capabilities),
                    GrantedUtc = group.Min(grant => grant.GrantedUtc),
                })
                .OrderBy(grant => grant.Path, LocalPath.Comparer)
                .ThenBy(grant => grant.Scope)
                .ToArray();
        }

        /// <summary>Checks whether a canonical path has a persisted grant for the requested capability.</summary>
        public bool IsGranted(string canonicalPath, LocalFileCapability capability)
        {
            foreach (var grant in this.GetGrants())
            {
                if ((grant.Capabilities & capability) != capability)
                {
                    continue;
                }

                if (grant.Scope == LocalFileGrantScope.File
                    && string.Equals(grant.Path, canonicalPath, Comparison()))
                {
                    return true;
                }

                if (grant.Scope == LocalFileGrantScope.Directory
                    && LocalPath.IsUnder(grant.Path, canonicalPath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Persists an Always-allow decision, merging capabilities into an existing grant.</summary>
        public void Allow(LocalFileGrant grant) => this.Update(new[] { grant }, Array.Empty<LocalFileGrant>());

        /// <summary>Revokes grants matching the given scope/path pairs.</summary>
        public void Revoke(IEnumerable<LocalFileGrant> removals) => this.Update(Array.Empty<LocalFileGrant>(), removals);

        /// <summary>Revokes every stored grant.</summary>
        public void RevokeAll() => this.Update(Array.Empty<LocalFileGrant>(), this.GetGrants());

        private void Update(IEnumerable<LocalFileGrant> additions, IEnumerable<LocalFileGrant> removals)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
            using var lease = this.AcquireWriteLease();
            var grants = this.GetGrants().ToList();
            foreach (var removal in removals)
            {
                grants.RemoveAll(grant =>
                    grant.Scope == removal.Scope
                    && string.Equals(grant.Path, removal.Path, Comparison()));
            }

            foreach (var addition in additions)
            {
                var existing = grants.FirstOrDefault(grant =>
                    grant.Scope == addition.Scope
                    && string.Equals(grant.Path, addition.Path, Comparison()));
                if (existing != null)
                {
                    existing.Capabilities |= addition.Capabilities;
                }
                else
                {
                    grants.Add(new LocalFileGrant
                    {
                        Scope = addition.Scope,
                        Path = addition.Path,
                        Capabilities = addition.Capabilities,
                        GrantedUtc = addition.GrantedUtc == default ? DateTimeOffset.UtcNow : addition.GrantedUtc,
                    });
                }
            }

            var temporary = this.path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(
                    grants.OrderBy(grant => grant.Path, LocalPath.Comparer), SerializerSettings));
                File.Move(temporary, this.path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static StringComparison Comparison() =>
            LocalPath.Comparer == StringComparer.OrdinalIgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        private FileStream AcquireWriteLease()
        {
            // All writers re-read after taking a cross-process lease; atomic rename keeps readers safe.
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return new FileStream(this.path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
