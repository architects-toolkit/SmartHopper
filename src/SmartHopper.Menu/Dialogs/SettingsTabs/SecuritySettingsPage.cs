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
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.Networking;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>Groups provider trust controls, network limits and revocable external-source permissions.</summary>
    internal sealed class SecuritySettingsPage : Panel
    {
        private const string AddOriginPlaceholder = "https://nextcloud.example.com:443";

        private readonly HashSet<string> initialOrigins;
        private readonly StackLayout originRows;
        private readonly NumericStepper httpTimeout;
        private readonly NumericStepper downloadLimit;
        private readonly HashSet<TextBox> removing = new HashSet<TextBox>();
        private TextBox pendingOrigin;

        /// <summary>Initializes the page with the provider trust/integrity controls.</summary>
        internal SecuritySettingsPage(Control providerSecurity)
        {
            this.initialOrigins = new HashSet<string>(ExternalSourceAllowlist.Current.GetOrigins(), StringComparer.Ordinal);
            this.originRows = new StackLayout { Orientation = Orientation.Vertical, Spacing = 4 };
            foreach (var origin in this.initialOrigins.OrderBy(value => value, StringComparer.Ordinal))
            {
                this.originRows.Items.Add(this.CreateOriginRow(origin));
            }

            this.pendingOrigin = this.CreatePendingOriginBox();
            this.originRows.Items.Add(this.pendingOrigin);

            var layout = new DynamicLayout { Spacing = new Size(10, 10), Padding = 10 };

            // Trusted providers + integrity mode (owned by the providers settings page).
            layout.Add(providerSecurity);

            // Network limits: HTTP timeout and the external download cap.
            layout.Add(new Panel { Height = 15 });
            layout.Add(new Label { Text = "Network Settings", Font = new Font(SystemFont.Bold, 12) });
            layout.Add(new Panel { Height = 5 });
            layout.Add(SettingsText.DescriptionLabel(
                "Configure global HTTP settings for all AI provider calls and external retrievals."));

            this.httpTimeout = new NumericStepper
            {
                MinValue = TimeoutDefaults.MinTimeoutSeconds,
                MaxValue = TimeoutDefaults.MaxTimeoutSeconds,
                Value = TimeoutDefaults.DefaultTimeoutSeconds,
                Width = 100,
            };
            var savedTimeout = SmartHopperSettings.Instance.GetSetting("Global", "TimeoutSeconds");
            if (savedTimeout is int timeoutSeconds)
            {
                this.httpTimeout.Value = timeoutSeconds;
            }

            layout.Add(new Label { Text = "HTTP timeout (seconds, default 300):" });
            layout.Add(this.httpTimeout);
            layout.Add(SettingsText.DescriptionLabel(
                "Timeout for AI operations including response generation, file uploads and downloads. Increase on slow connections or with large files.", fontSize: 9));

            this.downloadLimit = new NumericStepper
            {
                MinValue = 1,
                MaxValue = 1024,
                Value = Math.Clamp(SmartHopperSettings.Instance.ExternalDownloadLimitMiB, 1, 1024),
                Width = 100,
            };
            layout.Add(new Label { Text = "External download limit (MiB, default 16):" });
            layout.Add(this.downloadLimit);

            // Approved external sources stay at the bottom: the section is edited
            // row by row — type an origin and press Enter (or leave the field) to
            // add it; "−" removes that row; clearing a row also removes it.
            layout.Add(new Panel { Height = 15 });
            layout.Add(new Label { Text = "External data sources", Font = new Font(SystemFont.Bold, 12) });
            layout.Add(new Panel { Height = 5 });
            layout.Add(SettingsText.DescriptionLabel(
                "Unapproved local and remote sources ask before retrieval. Only Always allow decisions are saved here. " +
                "Permissions are exact origins, not wildcard domains or networks. Removing one takes effect after Save."));
            layout.Add(this.originRows);
            layout.Add(SettingsText.DescriptionLabel(
                "Approval permits retrieval; it does not grant separate permission to send private data to an AI service. " +
                "Configured AI-provider connections and sign-in traffic are managed separately."));

            // Trailing spacer absorbs the DynamicLayout's vertical stretch so the
            // origins list is not expanded (same pattern as the other pages).
            layout.Add(null);

            this.Content = new Scrollable { Content = layout };
        }

        /// <summary>Applies only removals so newly approved sources from other windows are preserved.</summary>
        internal void SaveSettings()
        {
            SmartHopperSettings.Instance.SetSetting("Global", "TimeoutSeconds", (int)this.httpTimeout.Value);
            SmartHopperSettings.Instance.ExternalDownloadLimitMiB = (int)this.downloadLimit.Value;

            var retained = this.originRows.Items
                .Select(item => (item.Control as StackLayout)?.Items.FirstOrDefault()?.Control as TextBox ?? item.Control as TextBox)
                .Where(box => box != null && !ReferenceEquals(box, this.pendingOrigin))
                .Select(box => box.Tag as string ?? box.Text?.Trim())
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var origin in retained.Where(origin => !this.initialOrigins.Contains(origin)))
            {
                ExternalSourceAllowlist.Current.Allow(new Uri(origin));
            }

            var revoked = this.initialOrigins.Where(origin => !retained.Contains(origin)).ToArray();
            if (revoked.Length != 0)
            {
                ExternalSourceAllowlist.Current.Revoke(revoked);
            }
        }

        /// <summary>
        /// Builds a committed origin row: an editable text field (Enter or focus
        /// loss re-validates) plus a "−" button that removes the permission.
        /// </summary>
        /// <param name="origin">The normalized origin this row holds.</param>
        private Control CreateOriginRow(string origin)
        {
            var box = new TextBox { Text = origin, Width = 420, Tag = origin };
            var remove = new Button { Text = "−", Width = 28, ToolTip = "Remove this permission" };

            // MouseDown on − must win over the field's LostFocus commit, otherwise an
            // invalid edit would pop a validation box before the row disappears.
            remove.MouseDown += (_, _) => this.removing.Add(box);
            remove.Click += (_, _) =>
            {
                this.removing.Remove(box);
                var item = this.originRows.Items.FirstOrDefault(i =>
                    (i.Control as StackLayout)?.Items.FirstOrDefault()?.Control == box);
                if (item != null)
                {
                    this.originRows.Items.Remove(item);
                }
            };

            box.KeyDown += (_, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    this.CommitOrigin(box, isPending: false);
                    e.Handled = true;
                }
            };
            box.LostFocus += (_, _) => this.CommitOrigin(box, isPending: false);

            var row = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            row.Items.Add(box);
            row.Items.Add(remove);
            return row;
        }

        /// <summary>Creates the trailing empty field that promotes into a committed row on entry.</summary>
        private TextBox CreatePendingOriginBox()
        {
            var box = new TextBox { PlaceholderText = AddOriginPlaceholder, Width = 420 };
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    this.CommitOrigin(box, isPending: true);
                    e.Handled = true;
                }
            };
            box.LostFocus += (_, _) => this.CommitOrigin(box, isPending: true);
            return box;
        }

        /// <summary>
        /// Validates and normalizes a typed origin. Committed rows restore their
        /// previous value on invalid input (or are removed when cleared); the
        /// pending row turns into a committed row and a fresh pending row appears.
        /// </summary>
        /// <param name="box">The text field being committed.</param>
        /// <param name="isPending">True for the trailing "add" field.</param>
        private void CommitOrigin(TextBox box, bool isPending)
        {
            if (this.removing.Contains(box))
            {
                return;
            }

            var text = box.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                if (!isPending)
                {
                    // Clearing a committed row means "remove this permission".
                    var item = this.originRows.Items.FirstOrDefault(i =>
                        (i.Control as StackLayout)?.Items.FirstOrDefault()?.Control == box);
                    if (item != null)
                    {
                        this.originRows.Items.Remove(item);
                    }
                }

                return;
            }

            string origin;
            try
            {
                var uri = new Uri(text, UriKind.Absolute);
                origin = ExternalSourceAllowlist.Origin(uri);
                if (uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                {
                    throw new ArgumentException();
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is UriFormatException)
            {
                MessageBox.Show(this, "Enter an exact HTTP(S) origin without credentials, a path, query or fragment.", "Invalid origin");
                box.Text = box.Tag as string ?? string.Empty;
                return;
            }

            var duplicate = this.originRows.Items
                .Select(item => (item.Control as StackLayout)?.Items.FirstOrDefault()?.Control as TextBox ?? item.Control as TextBox)
                .Any(other => other != null && !ReferenceEquals(other, box) && !ReferenceEquals(other, this.pendingOrigin)
                    && string.Equals(other.Tag as string ?? other.Text?.Trim(), origin, StringComparison.Ordinal));
            if (duplicate)
            {
                box.Text = box.Tag as string ?? string.Empty;
                return;
            }

            box.Text = origin;
            box.Tag = origin;
            if (isPending)
            {
                // Promote: this field becomes a committed row and a fresh empty
                // field reappears below for the next entry.
                this.originRows.Items.RemoveAt(this.originRows.Items.Count - 1);
                this.originRows.Items.Add(this.CreateOriginRow(origin));
                this.pendingOrigin = this.CreatePendingOriginBox();
                this.originRows.Items.Add(this.pendingOrigin);
            }
        }
    }
}
