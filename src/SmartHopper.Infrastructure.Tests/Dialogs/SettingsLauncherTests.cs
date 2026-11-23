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
    using SmartHopper.Infrastructure.Dialogs;
    using Xunit;

    /// <summary>
    /// Covers the settings deep-link indirection: registration toggles
    /// availability and <see cref="SettingsLauncher.Open"/> dispatches the tab
    /// selector to the registered opener. Each test resets the static callback
    /// for isolation.
    /// </summary>
    public class SettingsLauncherTests
    {
        [Fact(DisplayName = "Open_WithoutRegistration_IsNoOp")]
        public void Open_WithoutRegistration_IsNoOp()
        {
            SettingsLauncher.ResetForTests();

            Assert.False(SettingsLauncher.IsAvailable);
            SettingsLauncher.Open();
            SettingsLauncher.Open(SettingsLauncher.SecurityTab);
        }

        [Fact(DisplayName = "Open_AfterRegistration_InvokesCallbackWithTab")]
        public void Open_AfterRegistration_InvokesCallbackWithTab()
        {
            SettingsLauncher.ResetForTests();

            string? received = "unset";
            SettingsLauncher.RegisterOpenSettings(tab => received = tab);

            Assert.True(SettingsLauncher.IsAvailable);
            SettingsLauncher.Open(SettingsLauncher.SecurityTab);
            Assert.Equal(SettingsLauncher.SecurityTab, received);
        }

        [Fact(DisplayName = "Open_NullTab_PassesNullThrough")]
        public void Open_NullTab_PassesNullThrough()
        {
            SettingsLauncher.ResetForTests();

            var called = false;
            SettingsLauncher.RegisterOpenSettings(tab =>
            {
                called = true;
                Assert.Null(tab);
            });

            SettingsLauncher.Open();
            Assert.True(called);
        }

        [Fact(DisplayName = "RegisterOpenSettings_LastRegistrationWins")]
        public void RegisterOpenSettings_LastRegistrationWins()
        {
            SettingsLauncher.ResetForTests();

            var firstCalled = false;
            var secondCalled = false;
            SettingsLauncher.RegisterOpenSettings(_ => firstCalled = true);
            SettingsLauncher.RegisterOpenSettings(_ => secondCalled = true);

            SettingsLauncher.Open();

            Assert.False(firstCalled);
            Assert.True(secondCalled);
        }
    }
}
