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
using Grasshopper.Kernel;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for components that support selecting objects on the Grasshopper canvas.
    /// Uses <see cref="IGH_DocumentObject"/> to support all object types including scribbles
    /// which do not implement <see cref="IGH_ActiveObject"/>.
    /// </summary>
    public interface ISelectingComponent
    {
        List<IGH_DocumentObject> SelectedObjects { get; }

        void EnableSelectionMode();
    }
}
