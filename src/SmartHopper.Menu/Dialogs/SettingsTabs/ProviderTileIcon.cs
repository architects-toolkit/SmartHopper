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

using System.IO;
using Eto.Drawing;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Converts a provider's <see cref="IAIProvider.Icon"/> (a GDI icon) into an Eto
    /// <see cref="Bitmap"/> for settings tiles and trust rows. Returns null when the
    /// provider ships no icon or conversion fails — icon errors never block settings.
    /// </summary>
    internal static class ProviderTileIcon
    {
        /// <summary>Loads the provider icon as an Eto bitmap scaled to <paramref name="size"/>.</summary>
        /// <param name="provider">The provider whose icon is loaded.</param>
        /// <param name="size">Target edge length in pixels (square).</param>
        internal static Bitmap Load(IAIProvider provider, int size)
        {
            if (provider?.Icon == null)
            {
                return null;
            }

            try
            {
                using var ms = new MemoryStream();
                provider.Icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bitmap = new Bitmap(ms);

                // Never upscale: icons smaller than the tile keep their natural size.
                return bitmap.Width <= size && bitmap.Height <= size
                    ? bitmap
                    : new Bitmap(bitmap, size, size, ImageInterpolation.High);
            }
            catch
            {
                return null;
            }
        }
    }
}
