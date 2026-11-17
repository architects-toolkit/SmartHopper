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
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.ProviderSdk.Settings
{
    /// <summary>
    /// One row of a provider's settings surface — the ordered list the settings page renders.
    /// A surface mixes <see cref="BoundSettingElement"/> rows (persisted settings bound to the
    /// settings store) and <see cref="ChromeElement"/> rows (live, never-persisted UI such as
    /// status labels, action buttons and links).
    /// </summary>
    /// <remarks>
    /// Purity contract: <c>IAIProviderSettings.GetSurface()</c> and
    /// <c>GetSettingDescriptors()</c> must only construct elements and wire delegates — never
    /// invoke them. The storage layer enumerates descriptors while resolving secret flags, so a
    /// factory that reads live state (stored credentials, discovery caches) can re-enter settings
    /// reads; delegates keep that evaluation inside the host's render/repaint pass instead.
    /// </remarks>
    public abstract class ProviderSurfaceElement
    {
    }

    /// <summary>
    /// A persisted setting rendered as an editor row. Wraps the storage-facing
    /// <see cref="SettingDescriptor"/> with optional live view-state delegates evaluated by the
    /// host at render and repaint time — never by the settings store.
    /// </summary>
    public sealed class BoundSettingElement : ProviderSurfaceElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoundSettingElement"/> class.
        /// </summary>
        /// <param name="descriptor">The persisted setting's descriptor.</param>
        public BoundSettingElement(SettingDescriptor descriptor)
        {
            this.Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        /// <summary>Gets the persisted setting's descriptor (name, type, secret and hidden flags).</summary>
        public SettingDescriptor Descriptor { get; }

        /// <summary>
        /// Gets or sets the live default-value source used when no stored value exists.
        /// Falls back to <see cref="SettingDescriptor.DefaultValue"/> when unset.
        /// </summary>
        public Func<object?>? DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the live dropdown-option source. Falls back to
        /// <see cref="SettingDescriptor.AllowedValues"/> when unset.
        /// </summary>
        public Func<IEnumerable<object>?>? AllowedValues { get; set; }

        /// <summary>
        /// Gets or sets the live enabled-state source for the editor.
        /// Falls back to <see cref="SettingDescriptor.Enabled"/> when unset.
        /// </summary>
        public Func<bool>? Enabled { get; set; }

        /// <summary>
        /// Gets or sets an optional action button rendered beside the editor
        /// (e.g. a sync action next to a picker).
        /// </summary>
        public ActionChrome? Companion { get; set; }
    }

    /// <summary>
    /// Base class for live, non-persisted surface rows: they render UI chrome and are
    /// evaluated through delegates, but never produce settings-store keys.
    /// </summary>
    public abstract class ChromeElement : ProviderSurfaceElement
    {
        /// <summary>Gets or sets the row label, rendered like a setting display name.</summary>
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>
    /// A read-only, coloured status label fed by live delegates — for derived state
    /// such as connection status that must never be persisted.
    /// </summary>
    public sealed class StatusChrome : ChromeElement
    {
        /// <summary>Gets or sets the live status text source.</summary>
        public Func<string?>? Text { get; set; }

        /// <summary>
        /// Gets or sets the live semantic level source: "ok" (green), "busy" (orange),
        /// "error" (red); any other or missing value renders neutral grey.
        /// </summary>
        public Func<string?>? Level { get; set; }
    }

    /// <summary>
    /// A button that invokes a provider action directly with the current form settings —
    /// the clean replacement for pseudo-boolean settings submitted through
    /// <c>ValidateSettings</c> (e.g. connect/disconnect, sync).
    /// </summary>
    public sealed class ActionChrome : ChromeElement
    {
        /// <summary>Gets or sets the live button-text source.</summary>
        public Func<string?>? Text { get; set; }

        /// <summary>Gets or sets the live description source rendered under the row.</summary>
        public Func<string?>? Description { get; set; }

        /// <summary>Gets or sets the live enabled-state source. Defaults to enabled when unset.</summary>
        public Func<bool>? Enabled { get; set; }

        /// <summary>
        /// Gets or sets the action. Receives the current (already validated and persisted)
        /// form settings so it can honor fields the user just edited.
        /// </summary>
        public Func<IReadOnlyDictionary<string, object>, CancellationToken, Task>? InvokeAsync { get; set; }
    }

    /// <summary>
    /// A hyperlink row that opens <see cref="Url"/> in the user's browser.
    /// </summary>
    public sealed class LinkChrome : ChromeElement
    {
        /// <summary>Gets or sets the absolute http(s) URL opened when the link is clicked.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Gets or sets the link text. When empty, <see cref="Url"/> is displayed.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets the optional description rendered under the row.</summary>
        public string? Description { get; set; }
    }
}
