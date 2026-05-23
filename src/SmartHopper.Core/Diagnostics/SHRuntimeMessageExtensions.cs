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

using Grasshopper.Kernel;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Core.Diagnostics
{
    /// <summary>
    /// Extension methods for <see cref="SHRuntimeMessage"/> to convert to Grasshopper-specific types.
    /// </summary>
    public static class SHRuntimeMessageExtensions
    {
        /// <summary>
        /// Converts the message severity to Grasshopper's runtime message level.
        /// </summary>
        /// <param name="message">The runtime message.</param>
        /// <returns>The corresponding Grasshopper runtime message level.</returns>
        public static GH_RuntimeMessageLevel ToGrasshopperLevel(this SHRuntimeMessage message)
        {
            return message.Severity switch
            {
                SHRuntimeMessageSeverity.Error => GH_RuntimeMessageLevel.Error,
                SHRuntimeMessageSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                _ => GH_RuntimeMessageLevel.Remark,
            };
        }
    }
}
