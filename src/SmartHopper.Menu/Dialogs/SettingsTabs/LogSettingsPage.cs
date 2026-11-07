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

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    using Eto.Drawing;
    using Eto.Forms;
    using SmartHopper.Menu.Dialogs.SettingsTabs.Models;
    using SmartHopper.ProviderSdk.Logging;

    /// <summary>
    /// Logging settings tab: local log consent, sink toggles/levels, retention, and
    /// the collector shipping switch (which itself requires diagnostics consent).
    /// </summary>
    public class LogSettingsPage : Panel
    {
        private static readonly (ProviderLogLevel Level, string Label)[] Levels =
        {
            (ProviderLogLevel.Trace, "Trace"),
            (ProviderLogLevel.Info, "Info"),
            (ProviderLogLevel.Warning, "Warning"),
            (ProviderLogLevel.Error, "Error"),
        };

        private readonly DropDown _loggingEnabledDropDown;
        private readonly CheckBox _liveOutputCheckBox;
        private readonly DropDown _liveOutputLevelDropDown;
        private readonly CheckBox _logFileCheckBox;
        private readonly DropDown _logFileLevelDropDown;
        private readonly NumericStepper _retentionControl;
        private readonly CheckBox _otlpCheckBox;
        private readonly DropDown _otlpLevelDropDown;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogSettingsPage"/> class.
        /// </summary>
        public LogSettingsPage()
        {
            this._loggingEnabledDropDown = new DropDown();
            this._loggingEnabledDropDown.Items.Add(new ListItem { Text = "Follow diagnostics consent (default)" });
            this._loggingEnabledDropDown.Items.Add(new ListItem { Text = "On" });
            this._loggingEnabledDropDown.Items.Add(new ListItem { Text = "Off" });
            this._loggingEnabledDropDown.SelectedIndex = 0;

            this._liveOutputCheckBox = new CheckBox { Text = "Echo logs to Rhino command line" };
            this._liveOutputLevelDropDown = BuildLevelDropDown();
            this._logFileCheckBox = new CheckBox { Text = "Write logs to file" };
            this._logFileLevelDropDown = BuildLevelDropDown();
            this._otlpCheckBox = new CheckBox { Text = "Share logs with the telemetry collector" };
            this._otlpLevelDropDown = BuildLevelDropDown();
            this._retentionControl = new NumericStepper { MinValue = 1, MaxValue = 90, Increment = 1 };

            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            AddRow(layout, "Local logging:", this._loggingEnabledDropDown);
            AddDescription(layout, "Controls local (on-machine) logging. 'Follow diagnostics consent' keeps local logging aligned with the Share diagnostics option on the General tab; On/Off override it just for local logs.");
            layout.Add(new Panel { Height = 10 });

            layout.Add(this._liveOutputCheckBox);
            AddRow(layout, "Live level:", this._liveOutputLevelDropDown);
            AddDescription(layout, "Echoes log entries at or above the selected level to Rhino's command-line window.");
            layout.Add(new Panel { Height = 10 });

            layout.Add(this._logFileCheckBox);
            AddRow(layout, "File level:", this._logFileLevelDropDown);
            AddRow(layout, "Retention (days):", this._retentionControl);
            AddDescription(layout, "Writes log entries at or above the selected level to a rolling per-day log file under the diagnostics folder, keeping the last N days.");
            layout.Add(new Panel { Height = 10 });

            layout.Add(this._otlpCheckBox);
            AddRow(layout, "Share level:", this._otlpLevelDropDown);
            AddDescription(layout, "Ships log entries at or above the selected level to the telemetry collector for automated investigation. Requires 'Share diagnostics' on the General tab — nothing leaves this machine without that consent. Messages are scrubbed of personal data and stack traces are stripped of file paths.");

            layout.Add(null);
            this.Content = layout;
        }

        /// <summary>
        /// Loads settings into the controls.
        /// </summary>
        public void LoadSettings(LogSettings settings)
        {
            this._loggingEnabledDropDown.SelectedIndex = settings.LoggingEnabled switch
            {
                true => 1,
                false => 2,
                null => 0,
            };
            this._liveOutputCheckBox.Checked = settings.LiveOutputEnabled;
            SetLevel(this._liveOutputLevelDropDown, settings.LiveOutputLevel);
            this._logFileCheckBox.Checked = settings.LogFileEnabled;
            SetLevel(this._logFileLevelDropDown, settings.LogFileLevel);
            this._retentionControl.Value = settings.LogRetentionDays;
            this._otlpCheckBox.Checked = settings.OtlpLogEnabled;
            SetLevel(this._otlpLevelDropDown, settings.OtlpLogLevel);
        }

        /// <summary>
        /// Saves control values back into the model.
        /// </summary>
        public void SaveSettings(LogSettings settings)
        {
            settings.LoggingEnabled = this._loggingEnabledDropDown.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
            settings.LiveOutputEnabled = this._liveOutputCheckBox.Checked == true;
            settings.LiveOutputLevel = Levels[this._liveOutputLevelDropDown.SelectedIndex].Level;
            settings.LogFileEnabled = this._logFileCheckBox.Checked == true;
            settings.LogFileLevel = Levels[this._logFileLevelDropDown.SelectedIndex].Level;
            settings.LogRetentionDays = (int)this._retentionControl.Value;
            settings.OtlpLogEnabled = this._otlpCheckBox.Checked == true;
            settings.OtlpLogLevel = Levels[this._otlpLevelDropDown.SelectedIndex].Level;
        }

        private static DropDown BuildLevelDropDown()
        {
            var dropDown = new DropDown();
            foreach (var (_, label) in Levels)
            {
                dropDown.Items.Add(new ListItem { Text = label });
            }

            dropDown.SelectedIndex = 0;
            return dropDown;
        }

        private static void SetLevel(DropDown dropDown, ProviderLogLevel level)
        {
            for (var i = 0; i < Levels.Length; i++)
            {
                if (Levels[i].Level == level)
                {
                    dropDown.SelectedIndex = i;
                    return;
                }
            }

            dropDown.SelectedIndex = 0;
        }

        private static void AddRow(DynamicLayout layout, string label, Control control)
        {
            var row = new TableLayout { Spacing = new Size(10, 0) };
            row.Rows.Add(new TableRow(
                new TableCell(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(control, true)));
            layout.Add(row);
        }

        private static void AddDescription(DynamicLayout layout, string text)
        {
            layout.Add(new Label
            {
                Text = text,
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });
        }
    }
}
