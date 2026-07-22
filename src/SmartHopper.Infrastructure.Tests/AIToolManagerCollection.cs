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

namespace SmartHopper.Infrastructure.Tests
{
    using Xunit;

    /// <summary>
    /// xUnit collection that serializes test classes exercising the static
    /// <see cref="Infrastructure.AITools.AIToolManager"/> registry.
    ///
    /// <para>
    /// <c>AIToolManager</c> is a static class backed by a single shared tool
    /// dictionary. Test classes call <c>ResetTools()</c>/<c>RegisterTool()</c>
    /// on that shared state, so running them in parallel lets one class wipe or
    /// mutate tools another class just registered, producing intermittent
    /// failures (e.g. an executed tool returning <c>null</c>). Grouping the
    /// classes into a single xUnit collection disables parallel execution
    /// between them while still allowing the rest of the suite to run in
    /// parallel.
    /// </para>
    /// </summary>
    [CollectionDefinition("AIToolManager")]
    public sealed class AIToolManagerCollection
    {
    }
}
