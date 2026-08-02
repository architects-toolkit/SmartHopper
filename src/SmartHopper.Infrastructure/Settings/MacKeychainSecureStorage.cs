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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartHopper.Infrastructure.Settings
{
    /// <summary>
    /// Stores small secrets in the macOS Keychain using the
    /// <c>Security.framework</c> generic-password API.
    /// </summary>
    /// <remarks>
    /// This replaces the previous file-based XOR obfuscation, which used
    /// <see cref="System.Random"/> with a predictable seed derived from the
    /// user and machine names.
    /// </remarks>
    internal static class MacKeychainSecureStorage
    {
        private const string SecurityFrameworkPath = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundationFrameworkPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const string ServiceName = "SmartHopper";
        private const int NoError = 0;
        private const int DuplicateItemError = -25299; // errSecDuplicateItem

        [DllImport(SecurityFrameworkPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SecKeychainAddGenericPassword(
            IntPtr keychain,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            uint passwordLength,
            byte[] passwordData,
            out IntPtr itemRef);

        [DllImport(SecurityFrameworkPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SecKeychainFindGenericPassword(
            IntPtr keychain,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            out uint passwordLength,
            out IntPtr passwordData,
            out IntPtr itemRef);

        [DllImport(SecurityFrameworkPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SecKeychainItemFreeContent(
            IntPtr attrList,
            IntPtr data);

        [DllImport(SecurityFrameworkPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SecKeychainItemDelete(IntPtr itemRef);

        [DllImport(CoreFoundationFrameworkPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CFRelease(IntPtr cf);

        /// <summary>
        /// Stores the given data in the macOS Keychain, replacing any previous
        /// entry for the same <paramref name="keyName"/>.
        /// </summary>
        /// <param name="keyName">The account name under which the data is stored.</param>
        /// <param name="data">The raw bytes to store.</param>
        /// <returns><c>true</c> if the data was stored successfully; otherwise <c>false</c>.</returns>
        public static bool Store(string keyName, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(keyName) || data == null)
            {
                return false;
            }

            try
            {
                // Remove any existing entry so we can re-add it cleanly.
                if (TryFindItem(keyName, out IntPtr existingItem, out _, out IntPtr existingData))
                {
                    if (existingData != IntPtr.Zero)
                    {
                        SecKeychainItemFreeContent(IntPtr.Zero, existingData);
                    }

                    SecKeychainItemDelete(existingItem);
                    CFRelease(existingItem);
                }

                byte[] serviceName = Encoding.UTF8.GetBytes(ServiceName + "\0");
                byte[] accountName = Encoding.UTF8.GetBytes(keyName + "\0");

                int result = SecKeychainAddGenericPassword(
                    IntPtr.Zero,
                    (uint)serviceName.Length,
                    serviceName,
                    (uint)accountName.Length,
                    accountName,
                    (uint)data.Length,
                    data,
                    out IntPtr newItem);

                if (newItem != IntPtr.Zero)
                {
                    CFRelease(newItem);
                }

                if (result == DuplicateItemError)
                {
                    Debug.WriteLine($"[MacKeychainSecureStorage] Key '{keyName}' already exists; storage failed after duplicate removal.");
                    return false;
                }

                if (result != NoError)
                {
                    Debug.WriteLine($"[MacKeychainSecureStorage] Store failed for '{keyName}' with status {result}.");
                    return false;
                }

                Debug.WriteLine($"[MacKeychainSecureStorage] Stored data '{keyName}' in macOS Keychain.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MacKeychainSecureStorage] Store error for '{keyName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves data previously stored with <see cref="Store"/>.
        /// </summary>
        /// <param name="keyName">The account name to look up.</param>
        /// <returns>The stored bytes, or <c>null</c> if not found or an error occurs.</returns>
        public static byte[] Retrieve(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return null;
            }

            if (!TryFindItem(keyName, out IntPtr itemRef, out uint length, out IntPtr data))
            {
                return null;
            }

            try
            {
                if (length == 0 || data == IntPtr.Zero)
                {
                    return Array.Empty<byte>();
                }

                byte[] result = new byte[length];
                Marshal.Copy(data, result, 0, (int)length);
                Debug.WriteLine($"[MacKeychainSecureStorage] Retrieved data '{keyName}' from macOS Keychain.");
                return result;
            }
            finally
            {
                if (data != IntPtr.Zero)
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, data);
                }

                if (itemRef != IntPtr.Zero)
                {
                    CFRelease(itemRef);
                }
            }
        }

        /// <summary>
        /// Tries to locate a generic password item in the default keychain.
        /// </summary>
        /// <param name="keyName">The account name to look up.</param>
        /// <param name="itemRef">The keychain item reference, or <see cref="IntPtr.Zero"/>.</param>
        /// <param name="length">The length of the returned data.</param>
        /// <param name="data">A pointer to the returned data, or <see cref="IntPtr.Zero"/>.</param>
        /// <returns><c>true</c> if the item was found; otherwise <c>false</c>.</returns>
        private static bool TryFindItem(string keyName, out IntPtr itemRef, out uint length, out IntPtr data)
        {
            itemRef = IntPtr.Zero;
            length = 0;
            data = IntPtr.Zero;

            byte[] serviceName = Encoding.UTF8.GetBytes(ServiceName + "\0");
            byte[] accountName = Encoding.UTF8.GetBytes(keyName + "\0");

            int result = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceName.Length,
                serviceName,
                (uint)accountName.Length,
                accountName,
                out length,
                out data,
                out itemRef);

            return result == NoError;
        }
    }
}
