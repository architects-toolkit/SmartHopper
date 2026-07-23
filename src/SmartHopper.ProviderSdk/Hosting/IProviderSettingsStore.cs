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

using System.Collections.Generic;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Per-provider settings store. The host scopes each instance to a single provider
    /// id so a community provider cannot read or write secrets belonging to another
    /// provider through this surface.
    /// </summary>
    public interface IProviderSettingsStore
    {
        /// <summary>
        /// Provider id this store is scoped to.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Retrieve the full settings dictionary for this provider, or an empty dictionary
        /// when none have been persisted yet.
        /// </summary>
        IDictionary<string, object> GetAll();

        /// <summary>
        /// Persist the full settings dictionary for this provider.
        /// </summary>
        void SetAll(IDictionary<string, object> settings);

        /// <summary>
        /// Read a single setting by key. Returns <paramref name="defaultValue"/> when
        /// the key is missing or the stored value is not assignable to <typeparamref name="T"/>.
        /// </summary>
        T Get<T>(string key, T defaultValue = default);

        /// <summary>
        /// Write a single setting value by key.
        /// </summary>
        void Set<T>(string key, T value);
    }

    /// <summary>
    /// In-memory <see cref="IProviderSettingsStore"/> used when SDK code runs outside the
    /// SmartHopper host (unit tests, sample provider self-checks, etc.). Per provider id.
    /// </summary>
    public sealed class InMemoryProviderSettingsStore : IProviderSettingsStore
    {
        private readonly Dictionary<string, object> _storage = new Dictionary<string, object>();

        /// <summary>Initializes a new in-memory store scoped to <paramref name="providerName"/>.</summary>
        public InMemoryProviderSettingsStore(string providerName)
        {
            this.ProviderName = providerName ?? string.Empty;
        }

        /// <inheritdoc />
        public string ProviderName { get; }

        /// <inheritdoc />
        public IDictionary<string, object> GetAll() => new Dictionary<string, object>(this._storage);

        /// <inheritdoc />
        public void SetAll(IDictionary<string, object> settings)
        {
            this._storage.Clear();
            if (settings == null)
            {
                return;
            }

            foreach (var kvp in settings)
            {
                this._storage[kvp.Key] = kvp.Value;
            }
        }

        /// <inheritdoc />
        public T Get<T>(string key, T defaultValue = default)
        {
            if (!this._storage.TryGetValue(key, out var value) || value == null)
            {
                return defaultValue;
            }

            if (value is T typed)
            {
                return typed;
            }

            try
            {
                return (T)System.Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <inheritdoc />
        public void Set<T>(string key, T value) => this._storage[key] = value;
    }
}
