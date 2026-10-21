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
using System.Collections.Generic;
using System.Security.Cryptography;
namespace SmartHopper.ProviderSdk.Catalogue
{
    /// <summary>
    /// ES256 (ECDSA P-256 + SHA-256) detached-signature verification for
    /// catalogue artefacts. The wire signature is base64 of the IEEE-P1363
    /// (<c>r‖s</c>, 64 bytes) signature — verified natively on .NET 7 with no
    /// external crypto dependency.
    /// </summary>
    public static class CatalogueSignature
    {
        /// <summary>
        /// Verifies a detached base64 P1363 signature over <paramref name="data"/>
        /// with the given base64 SPKI DER public key.
        /// </summary>
        /// <returns>True only when the signature is cryptographically valid.</returns>
        public static bool Verify(byte[] data, string signatureBase64, string publicKeyBase64)
        {
            if (data == null || string.IsNullOrEmpty(signatureBase64) || string.IsNullOrEmpty(publicKeyBase64))
            {
                return false;
            }

            byte[] signature;
            byte[] publicKey;
            try
            {
                signature = Convert.FromBase64String(signatureBase64.Trim());
                publicKey = Convert.FromBase64String(publicKeyBase64);
            }
            catch (FormatException)
            {
                return false;
            }

            if (signature.Length != 64)
            {
                return false;
            }

            try
            {
                using (var ecdsa = ECDsa.Create())
                {
                    ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                    return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies <paramref name="signatureBase64"/> over <paramref name="data"/>
        /// against the trusted key named by <paramref name="keyId"/> — or, when the
        /// id is unknown, against every trusted key (cheap: signatures are 64 B).
        /// </summary>
        /// <returns>True when any trusted key validates the signature.</returns>
        public static bool VerifyWithTrustedKeys(
            byte[] data,
            string signatureBase64,
            string keyId,
            IReadOnlyDictionary<string, string> trustedKeys)
        {
            if (trustedKeys == null || trustedKeys.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(keyId) && trustedKeys.TryGetValue(keyId, out var named))
            {
                return Verify(data, signatureBase64, named);
            }

            foreach (var kv in trustedKeys)
            {
                if (Verify(data, signatureBase64, kv.Value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
