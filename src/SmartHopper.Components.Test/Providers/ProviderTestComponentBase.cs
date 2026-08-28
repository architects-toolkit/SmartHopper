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
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Shared setup/teardown base for the per-provider Grasshopper test components.
    /// Each derived component remains an independent test runner for a specific provider and feature;
    /// this base only centralizes common component wiring such as category, provider selection, and exposure.
    /// </summary>
    public abstract class ProviderTestComponentBase : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Gets the provider name that this test component exercises.
        /// </summary>
        protected abstract string TestProviderName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderTestComponentBase"/> class.
        /// </summary>
        /// <param name="name">The component display name.</param>
        /// <param name="nickname">The component nickname.</param>
        /// <param name="description">The component description.</param>
        /// <param name="category">The component category in the Grasshopper toolbar.</param>
        /// <param name="subCategory">The component subcategory in the Grasshopper toolbar.</param>
        protected ProviderTestComponentBase(
            string name,
            string nickname,
            string description,
            string category = "SmartHopper Tests",
            string subCategory = "Testing Providers")
            : base(name, nickname, description, category, subCategory)
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName(this.TestProviderName);
        }

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;
    }
}
