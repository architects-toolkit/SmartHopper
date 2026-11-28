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

namespace SmartHopper.Infrastructure.Networking
{
    /// <summary>Stores user-approved exact origins, never URLs, credentials or wildcard networks.</summary>
    public sealed class ExternalSourceAllowlist
    {
        private readonly string path;

        /// <summary>Gets the desktop user's persistent source permissions.</summary>
        public static ExternalSourceAllowlist Current { get; } = new ExternalSourceAllowlist(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper", "SmartHopper", "external-source-permissions.json"));

        /// <summary>Initializes a permission store at an explicit path, also used by isolated tests.</summary>
        public ExternalSourceAllowlist(string path)
        {
            this.path = Path.GetFullPath(path);
        }

        /// <summary>Canonicalizes an HTTP(S) origin without retaining any path, query or fragment.</summary>
        public static string Origin(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException("External sources require HTTP(S) without embedded credentials.");
            }

            return new UriBuilder(uri.Scheme, uri.IdnHost, uri.Port).Uri.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>Reads current grants on every lookup so revocations are not hidden by a cache.</summary>
        public IReadOnlyList<string> GetOrigins()
        {
            if (!File.Exists(this.path))
            {
                return Array.Empty<string>();
            }

            var origins = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(this.path))
                ?? throw new InvalidDataException("The external-source permission file is invalid.");
            return origins.Where(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    string.Equals(value, Origin(uri), StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Checks an exact scheme/hostname/port grant.</summary>
        public bool Contains(Uri uri) => this.GetOrigins().Contains(Origin(uri), StringComparer.Ordinal);

        /// <summary>Persists an explicit Always Allow decision.</summary>
        public void Allow(Uri uri) => this.Update(new[] { Origin(uri) }, Array.Empty<string>());

        /// <summary>Revokes selected origins without discarding grants added by another open dialog.</summary>
        public void Revoke(IEnumerable<string> origins) => this.Update(Array.Empty<string>(), origins);

        private void Update(IEnumerable<string> additions, IEnumerable<string> removals)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
            using var lease = this.AcquireWriteLease();
            var values = new HashSet<string>(this.GetOrigins(), StringComparer.Ordinal);
            values.ExceptWith(removals);
            values.UnionWith(additions);
            var temporary = this.path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(values.OrderBy(value => value, StringComparer.Ordinal)));
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
