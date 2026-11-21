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
using SmartHopper.Infrastructure.FileSystem;
using SmartHopper.Infrastructure.Networking;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>Groups provider trust controls, network limits and revocable external-source permissions.</summary>
    internal sealed class SecuritySettingsPage : Panel
    {
        private const string AddOriginPlaceholder = "https://nextcloud.example.com:443";
        private const string AddFileGrantPlaceholder = "RW folder C:\\Projects";

        private readonly HashSet<string> initialOrigins;
        private readonly StackLayout originRows;
        private readonly HashSet<string> initialFileGrants;
        private readonly StackLayout fileRows;
        private readonly NumericStepper httpTimeout;
        private readonly NumericStepper downloadLimit;
        private readonly HashSet<TextBox> removing = new HashSet<TextBox>();
        private TextBox pendingOrigin;
        private TextBox pendingFileGrant;

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

            this.initialFileGrants = new HashSet<string>(
                LocalFileAllowlist.Current.GetGrants().Select(GrantKey), LocalPath.Comparer);
            this.fileRows = new StackLayout { Orientation = Orientation.Vertical, Spacing = 4 };
            foreach (var key in this.initialFileGrants.OrderBy(value => value, LocalPath.Comparer))
            {
                this.fileRows.Items.Add(this.CreateFileGrantRow(key));
            }

            this.pendingFileGrant = this.CreatePendingFileGrantBox();
            this.fileRows.Items.Add(this.pendingFileGrant);

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

            // Approved local files and folders, same editing model as the origins above.
            layout.Add(new Panel { Height = 15 });
            layout.Add(new Label { Text = "Local file permissions", Font = new Font(SystemFont.Bold, 12) });
            layout.Add(new Panel { Height = 5 });
            layout.Add(SettingsText.DescriptionLabel(
                "Unapproved paths ask before the assistant reads, writes or opens them. Only Always allow decisions are saved here. " +
                "Each row is '<capabilities> <file|folder> <path>' — R reads, W writes, E opens runnable documents. " +
                "Protected locations (credentials, system folders, SmartHopper settings) can never be approved."));
            layout.Add(this.fileRows);
            layout.Add(SettingsText.DescriptionLabel(
                "Removing a permission takes effect after Save. Allow-once approvals are not listed."));

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

            var retainedGrants = this.fileRows.Items
                .Select(item => (item.Control as StackLayout)?.Items.FirstOrDefault()?.Control as TextBox ?? item.Control as TextBox)
                .Where(box => box != null && !ReferenceEquals(box, this.pendingFileGrant))
                .Select(box => box.Tag as string ?? box.Text?.Trim())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(LocalPath.Comparer);

            // Revoke before allowing: editing a row's capabilities changes its key, so
            // the old key must be removed before the edited one is re-added — adding
            // first would let the revoke wipe the edit.
            var revokedGrants = this.initialFileGrants.Where(key => !retainedGrants.Contains(key)).ToArray();
            if (revokedGrants.Length != 0)
            {
                LocalFileAllowlist.Current.Revoke(revokedGrants.Select(ParseGrantKey));
            }

            foreach (var key in retainedGrants.Where(key => !this.initialFileGrants.Contains(key)))
            {
                LocalFileAllowlist.Current.Allow(ParseGrantKey(key));
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

        /// <summary>Stable row key: capabilities letter set, scope word and canonical path.</summary>
        private static string GrantKey(LocalFileGrant grant) =>
            CapabilityLetters(grant.Capabilities) + " "
                + (grant.Scope == LocalFileGrantScope.Directory ? "folder" : "file") + " "
                + grant.Path;

        private static LocalFileGrant ParseGrantKey(string key)
        {
            var firstSpace = key.IndexOf(' ');
            var secondSpace = key.IndexOf(' ', firstSpace + 1);
            var grant = new LocalFileGrant
            {
                Capabilities = ParseCapabilities(key.Substring(0, firstSpace)),
                Scope = key.Substring(firstSpace + 1, secondSpace - firstSpace - 1) == "folder"
                    ? LocalFileGrantScope.Directory
                    : LocalFileGrantScope.File,
                Path = key.Substring(secondSpace + 1).Trim(),
                GrantedUtc = DateTimeOffset.UtcNow,
            };
            return grant;
        }

        private static string CapabilityLetters(LocalFileCapability capabilities)
        {
            var letters = string.Empty;
            if ((capabilities & LocalFileCapability.Read) != 0)
            {
                letters += "R";
            }

            if ((capabilities & LocalFileCapability.Write) != 0)
            {
                letters += "W";
            }

            if ((capabilities & LocalFileCapability.Execute) != 0)
            {
                letters += "E";
            }

            return letters;
        }

        private static LocalFileCapability ParseCapabilities(string letters)
        {
            var capabilities = LocalFileCapability.None;
            foreach (var letter in letters.ToUpperInvariant())
            {
                capabilities |= letter switch
                {
                    'R' => LocalFileCapability.Read,
                    'W' => LocalFileCapability.Write,
                    'E' => LocalFileCapability.Execute,
                    _ => throw new ArgumentException(),
                };
            }

            if (capabilities == LocalFileCapability.None)
            {
                throw new ArgumentException();
            }

            return capabilities;
        }

        /// <summary>Builds a committed file-permission row mirroring the origin rows.</summary>
        private Control CreateFileGrantRow(string key)
        {
            var box = new TextBox { Text = key, Width = 520, Tag = key };
            var remove = new Button { Text = "−", Width = 28, ToolTip = "Remove this permission" };

            remove.MouseDown += (_, _) => this.removing.Add(box);
            remove.Click += (_, _) =>
            {
                this.removing.Remove(box);
                var item = this.fileRows.Items.FirstOrDefault(i =>
                    (i.Control as StackLayout)?.Items.FirstOrDefault()?.Control == box);
                if (item != null)
                {
                    this.fileRows.Items.Remove(item);
                }
            };

            box.KeyDown += (_, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    this.CommitFileGrant(box, isPending: false);
                    e.Handled = true;
                }
            };
            box.LostFocus += (_, _) => this.CommitFileGrant(box, isPending: false);

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
        private TextBox CreatePendingFileGrantBox()
        {
            var box = new TextBox { PlaceholderText = AddFileGrantPlaceholder, Width = 520 };
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    this.CommitFileGrant(box, isPending: true);
                    e.Handled = true;
                }
            };
            box.LostFocus += (_, _) => this.CommitFileGrant(box, isPending: true);
            return box;
        }

        /// <summary>
        /// Validates a typed grant row of the form 'RWE file|folder path', canonicalizes the
        /// path and rejects denied-tier locations the consent prompt would refuse anyway.
        /// </summary>
        private void CommitFileGrant(TextBox box, bool isPending)
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
                    var item = this.fileRows.Items.FirstOrDefault(i =>
                        (i.Control as StackLayout)?.Items.FirstOrDefault()?.Control == box);
                    if (item != null)
                    {
                        this.fileRows.Items.Remove(item);
                    }
                }

                return;
            }

            string key;
            try
            {
                var firstSpace = text.IndexOf(' ');
                var secondSpace = firstSpace < 0 ? -1 : text.IndexOf(' ', firstSpace + 1);
                if (firstSpace < 0 || secondSpace < 0)
                {
                    throw new ArgumentException();
                }

                var capabilities = ParseCapabilities(text.Substring(0, firstSpace));
                var scopeWord = text.Substring(firstSpace + 1, secondSpace - firstSpace - 1).ToLowerInvariant();
                if (scopeWord != "file" && scopeWord != "folder")
                {
                    throw new ArgumentException();
                }

                if (!LocalPath.TryCanonicalize(text.Substring(secondSpace + 1), out var canonical, out _)
                    || LocalPath.IsDenied(canonical))
                {
                    throw new ArgumentException();
                }

                key = CapabilityLetters(capabilities) + " " + scopeWord + " " + canonical;
            }
            catch (ArgumentException)
            {
                MessageBox.Show(this,
                    "Enter '<capabilities> <file|folder> <absolute path>' using R, W and E letters. " +
                    "Protected locations cannot be approved.",
                    "Invalid file permission");
                box.Text = box.Tag as string ?? string.Empty;
                return;
            }

            var duplicate = this.fileRows.Items
                .Select(item => (item.Control as StackLayout)?.Items.FirstOrDefault()?.Control as TextBox ?? item.Control as TextBox)
                .Any(other => other != null && !ReferenceEquals(other, box) && !ReferenceEquals(other, this.pendingFileGrant)
                    && string.Equals(other.Tag as string ?? other.Text?.Trim(), key, Comparison()));
            if (duplicate)
            {
                box.Text = box.Tag as string ?? string.Empty;
                return;
            }

            box.Text = key;
            box.Tag = key;
            if (isPending)
            {
                this.fileRows.Items.RemoveAt(this.fileRows.Items.Count - 1);
                this.fileRows.Items.Add(this.CreateFileGrantRow(key));
                this.pendingFileGrant = this.CreatePendingFileGrantBox();
                this.fileRows.Items.Add(this.pendingFileGrant);
            }
        }

        private static StringComparison Comparison() =>
            LocalPath.Comparer == StringComparer.OrdinalIgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
    }
}
