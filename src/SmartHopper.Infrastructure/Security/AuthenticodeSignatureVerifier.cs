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
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SmartHopper.Infrastructure.Security
{
    /// <summary>Outcome of a real Authenticode file-signature verification.</summary>
    internal enum AuthenticodeSignatureStatus
    {
        /// <summary>Signature exists, covers the file bytes, and chains to a trusted root.</summary>
        Valid = 0,

        /// <summary>Signature exists and covers the file bytes, but the chain did not reach a trusted root (self-signed, expired, or unchainable cert). The publisher-pinning check must still pass.</summary>
        ValidUntrusted = 1,

        /// <summary>The file carries no Authenticode signature.</summary>
        Unsigned = 2,

        /// <summary>A signature exists but does not cover the bytes, or the file is malformed/explicitly distrusted.</summary>
        Invalid = 3,
    }

    /// <summary>
    /// Real Authenticode verification via <c>WinVerifyTrust</c> (Windows only). Unlike
    /// <see cref="X509Certificate.CreateFromSignedFile"/>, this validates that the signature
    /// actually covers the file bytes — a tampered file cannot pass by merely carrying the
    /// original certificate blob.
    /// </summary>
    internal static class AuthenticodeSignatureVerifier
    {
        // WinVerifyTrust LSTATUS values.
        private const int S_OK = 0;
        private const uint CERT_E_EXPIRED = 0x800B0101;
        private const uint CERT_E_UNTRUSTEDROOT = 0x800B0109;
        private const uint CERT_E_CHAINING = 0x800B010A;
        private const uint TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004;
        private const uint TRUST_E_NOSIGNATURE = 0x800B0100;

        // WINTRUST_DATA values.
        private const uint WTD_UI_NONE = 2;
        private const uint WTD_REVOKE_NONE = 0;
        private const uint WTD_CHOICE_FILE = 1;
        private const uint WTD_STATEACTION_VERIFY = 1;
        private const uint WTD_STATEACTION_CLOSE = 2;

        private static readonly Guid ActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        /// <summary>
        /// Verifies the embedded Authenticode signature of <paramref name="filePath"/>.
        /// Windows-only; callers must gate on <see cref="RuntimeInformation.IsOSPlatform"/>.
        /// </summary>
        /// <param name="filePath">Full path to the candidate file.</param>
        /// <returns>The signature status and the raw WinVerifyTrust code for diagnostics.</returns>
        internal static AuthenticodeSignatureStatus Verify(string filePath, out int rawStatus)
        {
            var fileInfo = new WintrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };

            var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
            var dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustData>());
            var data = new WintrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustData>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,

                // Revocation is not checked: the pinned signer is a non-CA signing
                // certificate with no revocation infrastructure, and offline use must
                // not break verification. Certificate identity is pinned separately.
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = fileInfoPtr,
                dwStateAction = WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = 0,
                dwUIContext = 0,
                pSignatureSettings = IntPtr.Zero,
            };

            try
            {
                Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);
                Marshal.StructureToPtr(data, dataPtr, false);
                var actionId = ActionGenericVerifyV2;
                rawStatus = WinVerifyTrust(IntPtr.Zero, ref actionId, dataPtr);
                data = Marshal.PtrToStructure<WintrustData>(dataPtr);
            }
            finally
            {
                // Release any trust-provider state data allocated during VERIFY.
                if (data.hWVTStateData != IntPtr.Zero)
                {
                    var close = new WintrustData
                    {
                        cbStruct = data.cbStruct,
                        dwUnionChoice = WTD_CHOICE_FILE,
                        pFile = fileInfoPtr,
                        dwStateAction = WTD_STATEACTION_CLOSE,
                        hWVTStateData = data.hWVTStateData,
                        dwUIChoice = WTD_UI_NONE,
                    };

                    Marshal.StructureToPtr(close, dataPtr, false);
                    var closeId = ActionGenericVerifyV2;
                    _ = WinVerifyTrust(IntPtr.Zero, ref closeId, dataPtr);
                }

                Marshal.FreeHGlobal(fileInfoPtr);
                Marshal.FreeHGlobal(dataPtr);
            }

            var status = (uint)rawStatus;
            if (rawStatus == S_OK)
            {
                return AuthenticodeSignatureStatus.Valid;
            }

            if (status == CERT_E_EXPIRED || status == CERT_E_UNTRUSTEDROOT ||
                status == CERT_E_CHAINING || status == TRUST_E_SUBJECT_NOT_TRUSTED)
            {
                // Integrity proven; chain trust is evaluated separately via thumbprint pinning.
                return AuthenticodeSignatureStatus.ValidUntrusted;
            }

            if (status == TRUST_E_NOSIGNATURE)
            {
                return AuthenticodeSignatureStatus.Unsigned;
            }

            return AuthenticodeSignatureStatus.Invalid;
        }

        /// <summary>
        /// Reads the embedded signer certificate of a signed file (Windows only). Returns
        /// <see langword="null"/> when the file carries no extractable signature.
        /// </summary>
        internal static X509Certificate2? GetSignerCertificate(string filePath)
        {
            try
            {
                return new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionId, IntPtr pWVTData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WintrustFileInfo
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WintrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }
    }
}
