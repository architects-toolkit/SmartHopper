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
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Properties;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Unified provider-trust dialog: one card per provider, shared between the two
    /// trust flows so consent decisions and integrity findings look and behave alike.
    /// <see cref="Decide"/> asks which newly detected providers may load;
    /// <see cref="ReportIssues"/> shows the integrity findings collected while loading.
    /// </summary>
    internal class ProviderTrustDialog : Dialog
    {
        private const int ContentWidth = 560;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;

        private readonly List<(NewProviderCandidate candidate, CheckBox check)> _rows =
            new List<(NewProviderCandidate, CheckBox)>();

        private readonly CheckBox _selectAll = new CheckBox { Text = "Select all" };

        /// <summary>Suppresses checkbox event re-entry while rows are being synced.</summary>
        private bool _syncing;

        /// <summary>True only when closed via "Enable selected" — X / "Skip all" enable nothing.</summary>
        private bool _applied;

        private readonly bool _reconsent;

        private ProviderTrustDialog(IReadOnlyList<NewProviderCandidate> candidates, bool reconsent)
        {
            this._reconsent = reconsent;
            this.InitShell(reconsent
                ? "Provider Changes Detected - SmartHopper"
                : "New AI Providers Detected - SmartHopper");
            this.Content = this.BuildContent(candidates);
        }

        private ProviderTrustDialog(IReadOnlyList<ProviderIntegrityIssue> issues)
        {
            this.InitShell("Provider Integrity Check - SmartHopper");
            this.Content = this.BuildReportContent(issues);
        }

        /// <summary>Applies the shared window chrome: title, fixed size, padding and icon.</summary>
        private void InitShell(string title)
        {
            this.Title = title;
            this.Resizable = false;
            this.Padding = new Padding(20);
            this.MinimumSize = new Size(ContentWidth + 80, 300);

            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Eto.Drawing.Icon(stream);
                }
            }
        }

        /// <summary>
        /// Shows the consent dialog and returns the enable decision per provider
        /// (<c>TrustedProviders</c> key → enabled). Closing without pressing
        /// "Enable selected" enables nothing.
        /// </summary>
        /// <param name="candidates">Provider candidates to review.</param>
        /// <param name="reconsent"><see langword="true"/> when the prompt re-confirms previously trusted providers whose files changed without verification.</param>
        public static Dictionary<string, bool> Decide(IReadOnlyList<NewProviderCandidate> candidates, bool reconsent = false)
        {
            var decisions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var dlg = new ProviderTrustDialog(candidates, reconsent);
            dlg.ShowModalInFront(RhinoEtoApp.MainWindow);
            foreach (var (candidate, check) in dlg._rows)
            {
                decisions[candidate.AssemblyName] = dlg._applied && check.Checked == true;
            }

            return decisions;
        }

        /// <summary>
        /// Shows the integrity findings collected during provider loading as a single
        /// read-only report — one card per provider instead of one modal per failure.
        /// </summary>
        /// <param name="issues">Integrity findings to display; empty lists are ignored.</param>
        public static void ReportIssues(IReadOnlyList<ProviderIntegrityIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return;
            }

            var dlg = new ProviderTrustDialog(issues);
            dlg.ShowModalInFront(RhinoEtoApp.MainWindow);
        }

        private static string VerificationLabel(ProviderVerificationResult? verification)
        {
            switch (verification?.Status)
            {
                case ProviderVerificationStatus.Match:
                    return "✓ verified";
                case ProviderVerificationStatus.Mismatch:
                    return "✗ hash mismatch";
                case ProviderVerificationStatus.NotFound:
                    return "? not in official manifest";
                case ProviderVerificationStatus.Unavailable:
                    return "? verification unavailable";
                default:
                    return "? unverified";
            }
        }

        private static Control BuildHeaderPanel(string heading, string subheading)
        {
            return new StackLayout
            {
                Spacing = 5,
                Items =
                {
                    new Label
                    {
                        Text = heading,
                        Font = new Font(SystemFont.Bold, 14),
                        TextColor = SystemColors.ControlText,
                    },
                    new Label
                    {
                        Text = subheading,
                        Font = new Font(SystemFont.Default, 10),
                        TextColor = SystemColors.DisabledText,
                        Wrap = WrapMode.Word,
                        Width = ContentWidth,
                    },
                },
            };
        }

        /// <summary>Wraps a row body in the shared white provider card chrome.</summary>
        private static Control WrapCard(Control content)
        {
            return new StackLayout
            {
                Spacing = 0,
                BackgroundColor = Colors.White,
                Padding = new Padding(10),
                Items = { content },
            };
        }

        private Control BuildContent(IReadOnlyList<NewProviderCandidate> candidates)
        {
            var list = new StackLayout { Spacing = 10 };
            foreach (var candidate in candidates)
            {
                list.Items.Add(this.BuildRow(candidate));
            }

            this._selectAll.Checked = candidates.Count > 0 && this.AllChecked();
            this._selectAll.CheckedChanged += (s, e) =>
            {
                if (!this._syncing)
                {
                    this.SetAll(this._selectAll.Checked == true);
                }
            };

            var count = candidates.Count;
            var heading = this._reconsent
                ? $"{count} enabled provider{(count == 1 ? string.Empty : "s")} changed"
                : $"{count} new AI provider{(count == 1 ? string.Empty : "s")} detected";
            var subheading = this._reconsent
                ? "These providers were enabled before, but their files changed and cannot be verified as official. Re-confirm the ones you still trust; unchecked providers are disabled for your security."
                : "Choose which providers SmartHopper may call. Unchecked providers stay disabled; you can enable them later in the SmartHopper settings.";

            var contentStack = new StackLayout
            {
                Spacing = 15,
                Items =
                {
                    BuildHeaderPanel(heading, subheading),
                    this._selectAll,
                    new Scrollable
                    {
                        Border = BorderType.None,
                        ExpandContentWidth = true,
                        Content = list,
                    },
                },
            };

            return new TableLayout
            {
                Spacing = new Size(0, 10),
                Rows =
                {
                    new TableRow(contentStack) { ScaleHeight = true },
                    new TableRow(this.BuildButtonPanel()),
                },
            };
        }

        private Control BuildReportContent(IReadOnlyList<ProviderIntegrityIssue> issues)
        {
            var list = new StackLayout { Spacing = 10 };
            foreach (var issue in issues)
            {
                list.Items.Add(BuildIssueRow(issue));
            }

            var blocked = issues.Count(i => i.Severity == ProviderIntegrityIssueSeverity.Error);
            var warned = issues.Count - blocked;

            var heading = $"{issues.Count} provider{(issues.Count == 1 ? string.Empty : "s")} failed integrity verification";
            var effects = new List<string>();
            if (blocked > 0)
            {
                effects.Add($"{blocked} blocked and not loaded");
            }

            if (warned > 0)
            {
                effects.Add($"{warned} loaded with a warning");
            }

            var settingsHint = SettingsLauncher.IsAvailable
                ? " Use Open Settings to review trust and integrity options directly."
                : string.Empty;
            var subheading =
                $"{string.Join("; ", effects)}. Review each provider below — re-download affected files from official SmartHopper sources, then manage them in SmartHopper → Settings → Security.{settingsHint}";

            var contentStack = new StackLayout
            {
                Spacing = 15,
                Items =
                {
                    BuildHeaderPanel(heading, subheading),
                    new Scrollable
                    {
                        Border = BorderType.None,
                        ExpandContentWidth = true,
                        Content = list,
                    },
                },
            };

            var okButton = new Button { Text = "OK", MinimumSize = new Size(80, 30) };
            okButton.Click += (s, e) => this.Close();
            this.DefaultButton = okButton;
            this.AbortButton = okButton;

            var buttonPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Right,
            };

            if (SettingsLauncher.IsAvailable)
            {
                var openSettingsButton = new Button { Text = "Open Settings", MinimumSize = new Size(110, 30) };
                openSettingsButton.Click += (s, e) =>
                {
                    // Close this modal first, then open Settings on the Security tab
                    // once the modal stack has unwound.
                    this.Close();
                    Application.Instance.AsyncInvoke(() => SettingsLauncher.Open(SettingsLauncher.SecurityTab));
                };
                buttonPanel.Items.Add(openSettingsButton);
            }

            buttonPanel.Items.Add(okButton);

            return new TableLayout
            {
                Spacing = new Size(0, 10),
                Rows =
                {
                    new TableRow(contentStack) { ScaleHeight = true },
                    new TableRow(buttonPanel),
                },
            };
        }

        private Control BuildRow(NewProviderCandidate candidate)
        {
            var check = new CheckBox
            {
                Checked = candidate.Verification?.Status == ProviderVerificationStatus.Match,
            };
            check.CheckedChanged += (s, e) =>
            {
                if (this._syncing)
                {
                    return;
                }

                this._syncing = true;
                this._selectAll.Checked = this.AllChecked();
                this._syncing = false;
            };
            this._rows.Add((candidate, check));

            var iconView = new ImageView { Size = new Size(16, 16) };

            var metaParts = new List<string>
            {
                VerificationLabel(candidate.Verification),
                $"Updated: {candidate.LastUpdate:yyyy-MM-dd}",
                $"Models (declared): {(candidate.ModelCount.HasValue ? candidate.ModelCount.Value.ToString() : "unknown")}",
            };

            var labels = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                Items =
                {
                    new Label
                    {
                        Text = candidate.DisplayName,
                        Font = new Font(SystemFont.Bold, 11),
                        TextColor = SystemColors.ControlText,
                    },
                    new Label
                    {
                        Text = string.Join("   •   ", metaParts),
                        Font = new Font(SystemFont.Default, 9),
                        TextColor = SystemColors.DisabledText,
                        Wrap = WrapMode.Word,
                    },
                },
            };

            if (!string.IsNullOrEmpty(candidate.TrustNote))
            {
                labels.Items.Add(new Label
                {
                    Text = candidate.TrustNote,
                    Font = new Font(SystemFont.Default, 9),
                    TextColor = Colors.OrangeRed,
                    Wrap = WrapMode.Word,
                    Width = ContentWidth - 60,
                });
            }

            var row = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(check, false),
                        new TableCell(iconView, false),
                        new TableCell(labels, true)),
                },
            };

            return WrapCard(row);
        }

        private static Control BuildIssueRow(ProviderIntegrityIssue issue)
        {
            var blocked = issue.Severity == ProviderIntegrityIssueSeverity.Error;
            var accent = blocked ? Colors.OrangeRed : Colors.DarkOrange;

            var severityGlyph = new Label
            {
                Text = blocked ? "✗" : "⚠",
                Font = new Font(SystemFont.Bold, 12),
                TextColor = accent,
            };

            var labels = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                Items =
                {
                    new Label
                    {
                        Text = issue.FileName,
                        Font = new Font(SystemFont.Bold, 11),
                        TextColor = SystemColors.ControlText,
                    },
                    new Label
                    {
                        Text = issue.Status,
                        Font = new Font(SystemFont.Default, 9),
                        TextColor = accent,
                        Wrap = WrapMode.Word,
                    },
                    new Label
                    {
                        Text = issue.Detail,
                        Font = new Font(SystemFont.Default, 9),
                        TextColor = SystemColors.DisabledText,
                        Wrap = WrapMode.Word,
                        Width = ContentWidth - 60,
                    },
                },
            };

            var row = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(severityGlyph, false),
                        new TableCell(labels, true)),
                },
            };

            return WrapCard(row);
        }

        private bool AllChecked()
        {
            foreach (var (_, check) in this._rows)
            {
                if (check.Checked != true)
                {
                    return false;
                }
            }

            return this._rows.Count > 0;
        }

        private void SetAll(bool value)
        {
            this._syncing = true;
            foreach (var (_, check) in this._rows)
            {
                check.Checked = value;
            }

            this._syncing = false;
        }

        private Control BuildButtonPanel()
        {
            var enableButton = new Button { Text = "Enable selected", MinimumSize = new Size(120, 30) };
            enableButton.Click += (s, e) =>
            {
                this._applied = true;
                this.Close();
            };
            this.DefaultButton = enableButton;

            var skipButton = new Button { Text = "Skip all", MinimumSize = new Size(80, 30) };
            skipButton.Click += (s, e) => this.Close();
            this.AbortButton = skipButton;

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Items = { skipButton, enableButton },
            };
        }
    }
}
