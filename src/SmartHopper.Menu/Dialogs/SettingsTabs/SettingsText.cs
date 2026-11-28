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
using System.Text;
using Eto.Drawing;
using Eto.Forms;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Produces description labels whose text is wrapped by measurement instead of relying on
    /// <see cref="Label.Wrap"/>: a wrapping label reports its preferred width as the full
    /// unwrapped text, so inside a Scrollable it becomes the widest element, stretches every
    /// horizontally-scaled sibling past the viewport, and forces a horizontal scrollbar.
    /// Pre-wrapping the text keeps the real preferred width bounded.
    /// </summary>
    internal static class SettingsText
    {
        internal const int DescriptionWidth = 500;

        /// <summary>Creates a grey description label pre-wrapped to <paramref name="width"/> pixels.</summary>
        internal static Label DescriptionLabel(string text, int width = DescriptionWidth, float fontSize = 10, Color? color = null)
        {
            var font = new Font(SystemFont.Default, fontSize);
            return new Label
            {
                Text = WrapToWidth(text, font, width),
                TextColor = color ?? Colors.Gray,
                Font = font,
            };
        }

        /// <summary>Word-wraps <paramref name="text"/> at <paramref name="maxWidth"/> pixels using real font measurement.</summary>
        internal static string WrapToWidth(string text, Font font, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                var line = new StringBuilder();
                foreach (var word in rawLine.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && font.MeasureString(candidate).Width > maxWidth)
                    {
                        result.Append(line).Append('\n');
                        line.Clear().Append(word);
                    }
                    else
                    {
                        line.Clear().Append(candidate);
                    }
                }

                result.Append(line);
            }

            return result.ToString();
        }
    }
}
