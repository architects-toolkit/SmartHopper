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
using System.Windows.Forms;
using SmartHopper.Core.UI.Chat;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Menu.Items
{
    internal static class OpenNewChatMenuItem
    {
        /// <summary>
        /// Creates an "Open a new chat" menu item that opens a new assistant web chat
        /// dialog on each click. Every invocation uses a fresh dialog ID so the user
        /// can keep multiple parallel conversations open.
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to open a new chat dialog.</returns>
        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Open a new chat");
            item.Click += (sender, e) =>
            {
                try
                {
                    _ = WebChatUtils.OpenAssistantChatDialogAsync(Guid.NewGuid(), "menu-chat");
                }
                catch (Exception ex)
                {
                    Logger.For<SmartHopperMenu>().Exception(ex, $"[SmartHopperMenu] Failed to open a new chat: {ex.Message}");
                }
            };
            return item;
        }
    }
}
