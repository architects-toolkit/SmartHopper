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
    using System.IO;
    using System.Text;
    using SmartHopper.Infrastructure.AIProviders;
    using Xunit;

    public class ProviderHashVerifierTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_SameFile_ReturnsSameHash [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_SameFile_ReturnsSameHash [Core]")]
#endif
        public void CalculateFileHash_SameFile_ReturnsSameHash()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test content");

                // Act
                var hash1 = ProviderHashVerifier.CalculateFileHash(tempFile);
                var hash2 = ProviderHashVerifier.CalculateFileHash(tempFile);

                // Assert
                Assert.Equal(hash1, hash2);
                Assert.NotEmpty(hash1);
                Assert.Equal(64, hash1.Length); // SHA-256 hex string length
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_DifferentFiles_ReturnsDifferentHashes [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_DifferentFiles_ReturnsDifferentHashes [Core]")]
#endif
        public void CalculateFileHash_DifferentFiles_ReturnsDifferentHashes()
        {
            // Arrange
            var tempFile1 = Path.GetTempFileName();
            var tempFile2 = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile1, "content 1");
                File.WriteAllText(tempFile2, "content 2");

                // Act
                var hash1 = ProviderHashVerifier.CalculateFileHash(tempFile1);
                var hash2 = ProviderHashVerifier.CalculateFileHash(tempFile2);

                // Assert
                Assert.NotEqual(hash1, hash2);
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_ReturnsLowercaseHex [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_ReturnsLowercaseHex [Core]")]
#endif
        public void CalculateFileHash_ReturnsLowercaseHex()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test");

                // Act
                var hash = ProviderHashVerifier.CalculateFileHash(tempFile);

                // Assert
                Assert.Equal(hash, hash.ToLower());
                Assert.Matches("^[0-9a-f]{64}$", hash); // Lowercase hex pattern
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_KnownContent_ReturnsExpectedHash [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_KnownContent_ReturnsExpectedHash [Core]")]
#endif
        public void CalculateFileHash_KnownContent_ReturnsExpectedHash()
        {
            // Arrange - SHA-256 of "test" is known
            var tempFile = Path.GetTempFileName();
            try
            {
                // Delete and recreate to ensure clean state (GetTempFileName creates a 0-byte file)
                File.Delete(tempFile);
                File.WriteAllBytes(tempFile, Encoding.UTF8.GetBytes("test"));

                // Act
                var hash = ProviderHashVerifier.CalculateFileHash(tempFile);

                // Assert - This is the SHA-256 hash of "test"
                Assert.Equal("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", hash);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_EmptyFile_ReturnsHash [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_EmptyFile_ReturnsHash [Core]")]
#endif
        public void CalculateFileHash_EmptyFile_ReturnsHash()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, string.Empty);

                // Act
                var hash = ProviderHashVerifier.CalculateFileHash(tempFile);

                // Assert - SHA-256 of empty string
                Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_BinaryContent_ReturnsHash [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_BinaryContent_ReturnsHash [Core]")]
#endif
        public void CalculateFileHash_BinaryContent_ReturnsHash()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                byte[] binaryContent = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
                File.WriteAllBytes(tempFile, binaryContent);

                // Act
                var hash = ProviderHashVerifier.CalculateFileHash(tempFile);

                // Assert
                Assert.NotEmpty(hash);
                Assert.Equal(64, hash.Length);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_LargeFile_ReturnsHash [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_LargeFile_ReturnsHash [Core]")]
#endif
        public void CalculateFileHash_LargeFile_ReturnsHash()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a 1MB file
                byte[] largeContent = new byte[1024 * 1024];
                for (int i = 0; i < largeContent.Length; i++)
                {
                    largeContent[i] = (byte)(i % 256);
                }

                File.WriteAllBytes(tempFile, largeContent);

                // Act
                var hash = ProviderHashVerifier.CalculateFileHash(tempFile);

                // Assert
                Assert.NotEmpty(hash);
                Assert.Equal(64, hash.Length);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CalculateFileHash_NonexistentFile_ThrowsException [Windows]")]
#else
        [Fact(DisplayName = "CalculateFileHash_NonexistentFile_ThrowsException [Core]")]
#endif
        public void CalculateFileHash_NonexistentFile_ThrowsException()
        {
            // Arrange
            var nonexistentFile = Path.Combine(Path.GetTempPath(), "nonexistent_file_12345.dll");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
            {
                ProviderHashVerifier.CalculateFileHash(nonexistentFile);
            });
        }

        // Note: Testing VerifyProviderAsync would require mocking HttpClient
        // or using integration tests with actual network calls.
        // For unit tests, we focus on the hash calculation logic.
        // Integration tests can be added separately to test the full verification flow.
    }
}
