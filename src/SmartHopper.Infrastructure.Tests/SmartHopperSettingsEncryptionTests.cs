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
    using System;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using SmartHopper.Infrastructure.Settings;
    using Xunit;

    /// <summary>
    /// Tests for the SH04: AES-GCM secret envelope used by <see cref="SmartHopperSettings"/>.
    /// The encrypt/decrypt helpers are private static, so they are exercised through reflection;
    /// no OS secure store or settings file is touched. GCM-specific tests return early where
    /// <see cref="AesGcm.IsSupported"/> is false (net7.0 on macOS), matching the runtime fallback
    /// that keeps writing SH03: CBC envelopes on those platforms.
    /// </summary>
    public class SmartHopperSettingsEncryptionTests
    {
        private static readonly byte[] TestKey = new byte[32];

        static SmartHopperSettingsEncryptionTests()
        {
            RandomNumberGenerator.Fill(TestKey);
        }

        private static string EncryptWithSecureKey(string plainText, byte[] key)
        {
            var method = typeof(SmartHopperSettings).GetMethod("EncryptWithSecureKey", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null, new object[] { plainText, key })!;
        }

        private static string DecryptGcmWithSecureKey(string encryptedData, byte[] key)
        {
            var method = typeof(SmartHopperSettings).GetMethod("DecryptGcmWithSecureKey", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            try
            {
                return (string)method!.Invoke(null, new object[] { encryptedData, key })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static string EncryptCbcWithSecureKey(string plainText, byte[] key)
        {
            var method = typeof(SmartHopperSettings).GetMethod("EncryptCbcWithSecureKey", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null, new object[] { plainText, key })!;
        }

        private static string DecryptCbcWithSecureKey(string encryptedData, byte[] key)
        {
            var method = typeof(SmartHopperSettings).GetMethod("DecryptCbcWithSecureKey", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null, new object[] { encryptedData, key })!;
        }

        [Fact]
        public void Encrypt_ProducesSH04PrefixedGcmEnvelope()
        {
            if (!AesGcm.IsSupported)
            {
                return;
            }

            var encrypted = EncryptWithSecureKey("sk-test-secret", TestKey);

            Assert.StartsWith("SH04:", encrypted, StringComparison.Ordinal);

            var envelope = Convert.FromBase64String(encrypted.Substring("SH04:".Length));
            const string plain = "sk-test-secret";

            // [nonce(12)][tag(16)][ciphertext]
            Assert.Equal(12 + 16 + Encoding.UTF8.GetByteCount(plain), envelope.Length);
        }

        [Fact]
        public void EncryptDecryptGcm_RoundTripsPlainText()
        {
            if (!AesGcm.IsSupported)
            {
                return;
            }

            const string secret = "sk-live-abcdef0123456789";
            var encrypted = EncryptWithSecureKey(secret, TestKey);
            var decrypted = DecryptGcmWithSecureKey(encrypted.Substring("SH04:".Length), TestKey);

            Assert.Equal(secret, decrypted);
        }

        [Fact]
        public void Encrypt_ProducesUniqueNoncePerCall()
        {
            if (!AesGcm.IsSupported)
            {
                return;
            }

            var first = EncryptWithSecureKey("same-secret", TestKey);
            var second = EncryptWithSecureKey("same-secret", TestKey);

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void DecryptGcm_RejectsTamperedCiphertext()
        {
            if (!AesGcm.IsSupported)
            {
                return;
            }

            var encrypted = EncryptWithSecureKey("sk-test-secret", TestKey);
            var envelope = Convert.FromBase64String(encrypted.Substring("SH04:".Length));

            // Flip one bit inside the ciphertext (after nonce + tag).
            envelope[envelope.Length - 1] ^= 0x01;

            Assert.Throws<CryptographicException>(() =>
                DecryptGcmWithSecureKey(Convert.ToBase64String(envelope), TestKey));
        }

        [Fact]
        public void DecryptGcm_RejectsTamperedTag()
        {
            if (!AesGcm.IsSupported)
            {
                return;
            }

            var encrypted = EncryptWithSecureKey("sk-test-secret", TestKey);
            var envelope = Convert.FromBase64String(encrypted.Substring("SH04:".Length));

            // Flip one bit inside the tag (bytes 12..27).
            envelope[12] ^= 0x01;

            Assert.Throws<CryptographicException>(() =>
                DecryptGcmWithSecureKey(Convert.ToBase64String(envelope), TestKey));
        }

        [Fact]
        public void DecryptGcm_RejectsTruncatedEnvelope()
        {
            var tooShort = Convert.ToBase64String(new byte[12 + 16 - 1]);

            Assert.Throws<CryptographicException>(() =>
                DecryptGcmWithSecureKey(tooShort, TestKey));
        }

        [Fact]
        public void DecryptCbc_StillReadsLegacyEnvelope()
        {
            // Encrypt with the legacy AES-CBC layout [iv(16)][ciphertext] as produced by SH02:/SH03:.
            const string secret = "sk-legacy-secret";
            byte[] envelope;
            using (var aes = Aes.Create())
            {
                aes.Key = TestKey;
                aes.GenerateIV();
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    var plainBytes = Encoding.UTF8.GetBytes(secret);
                    cs.Write(plainBytes, 0, plainBytes.Length);
                    cs.FlushFinalBlock();
                    var cipher = ms.ToArray();

                    envelope = new byte[aes.IV.Length + cipher.Length];
                    Array.Copy(aes.IV, 0, envelope, 0, aes.IV.Length);
                    Array.Copy(cipher, 0, envelope, aes.IV.Length, cipher.Length);
                }
            }

            var decrypted = DecryptCbcWithSecureKey(Convert.ToBase64String(envelope), TestKey);

            Assert.Equal(secret, decrypted);
        }

        [Fact]
        public void EncryptDecryptCbc_RoundTripsPlainText()
        {
            // Exercises the fallback write path used where AesGcm is unsupported (net7.0
            // on macOS). Runs everywhere — CBC is available on all target platforms.
            const string secret = "sk-cbc-fallback-secret";
            var encrypted = EncryptCbcWithSecureKey(secret, TestKey);

            Assert.StartsWith("SH03:", encrypted, StringComparison.Ordinal);

            var decrypted = DecryptCbcWithSecureKey(encrypted.Substring("SH03:".Length), TestKey);

            Assert.Equal(secret, decrypted);
        }
    }
}
