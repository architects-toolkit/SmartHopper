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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils
{
    /// <summary>
    /// Unit tests for CSharpIdentifierHelper functionality.
    /// Verifies sanitization and unsanitization work correctly for C# identifiers.
    /// </summary>
    public class CSharpIdentifierHelperTests
    {
        /// <summary>
        /// Tests that reserved words are properly sanitized with @ prefix.
        /// </summary>
        [Fact]
        public void SanitizeIdentifier_ReservedWords_ShouldAddAtPrefix()
        {
            // Test common reserved words
            Assert.Equal("@out", CSharpIdentifierHelper.SanitizeIdentifier("out"));
            Assert.Equal("@ref", CSharpIdentifierHelper.SanitizeIdentifier("ref"));
            Assert.Equal("@class", CSharpIdentifierHelper.SanitizeIdentifier("class"));
            Assert.Equal("@public", CSharpIdentifierHelper.SanitizeIdentifier("public"));
            Assert.Equal("@var", CSharpIdentifierHelper.SanitizeIdentifier("var"));
        }

        /// <summary>
        /// Tests that non-reserved words remain unchanged.
        /// </summary>
        [Fact]
        public void SanitizeIdentifier_NonReservedWords_ShouldRemainUnchanged()
        {
            // Test normal identifiers
            Assert.Equal("myVariable", CSharpIdentifierHelper.SanitizeIdentifier("myVariable"));
            Assert.Equal("test", CSharpIdentifierHelper.SanitizeIdentifier("test"));
            Assert.Equal("output", CSharpIdentifierHelper.SanitizeIdentifier("output"));
            Assert.Equal("className", CSharpIdentifierHelper.SanitizeIdentifier("className"));
        }

        /// <summary>
        /// Tests that invalid characters are replaced with underscores.
        /// </summary>
        [Fact]
        public void SanitizeIdentifier_InvalidCharacters_ShouldReplaceWithUnderscore()
        {
            Assert.Equal("my_variable", CSharpIdentifierHelper.SanitizeIdentifier("my-variable"));
            Assert.Equal("test_identifier", CSharpIdentifierHelper.SanitizeIdentifier("test identifier"));
            Assert.Equal("var_name", CSharpIdentifierHelper.SanitizeIdentifier("var name"));
        }

        /// <summary>
        /// Tests that identifiers starting with digits get underscore prefix.
        /// </summary>
        [Fact]
        public void SanitizeIdentifier_StartsWithDigit_ShouldAddUnderscorePrefix()
        {
            Assert.Equal("_123var", CSharpIdentifierHelper.SanitizeIdentifier("123var"));
            Assert.Equal("_9test", CSharpIdentifierHelper.SanitizeIdentifier("9test"));
            Assert.Equal("_0", CSharpIdentifierHelper.SanitizeIdentifier("0"));
        }

        /// <summary>
        /// Tests that null or whitespace identifiers are returned as-is.
        /// </summary>
        [Fact]
        public void SanitizeIdentifier_NullOrWhitespace_ShouldReturnAsIs()
        {
            Assert.Null(CSharpIdentifierHelper.SanitizeIdentifier(null));
            Assert.Equal("", CSharpIdentifierHelper.SanitizeIdentifier(""));
            Assert.Equal("   ", CSharpIdentifierHelper.SanitizeIdentifier("   "));
        }

        /// <summary>
        /// Tests that sanitized reserved words are properly unsanitized.
        /// </summary>
        [Fact]
        public void UnsanitizeIdentifier_SanitizedReservedWords_ShouldRemoveAtPrefix()
        {
            Assert.Equal("out", CSharpIdentifierHelper.UnsanitizeIdentifier("@out"));
            Assert.Equal("ref", CSharpIdentifierHelper.UnsanitizeIdentifier("@ref"));
            Assert.Equal("class", CSharpIdentifierHelper.UnsanitizeIdentifier("@class"));
            Assert.Equal("public", CSharpIdentifierHelper.UnsanitizeIdentifier("@public"));
        }

        /// <summary>
        /// Tests that non-sanitized identifiers remain unchanged.
        /// </summary>
        [Fact]
        public void UnsanitizeIdentifier_NonSanitizedIdentifiers_ShouldRemainUnchanged()
        {
            Assert.Equal("myVariable", CSharpIdentifierHelper.UnsanitizeIdentifier("myVariable"));
            Assert.Equal("test", CSharpIdentifierHelper.UnsanitizeIdentifier("test"));
            Assert.Equal("@custom", CSharpIdentifierHelper.UnsanitizeIdentifier("@custom")); // @custom is not a reserved word
        }

        /// <summary>
        /// Tests that null or whitespace identifiers are returned as-is.
        /// </summary>
        [Fact]
        public void UnsanitizeIdentifier_NullOrWhitespace_ShouldReturnAsIs()
        {
            Assert.Null(CSharpIdentifierHelper.UnsanitizeIdentifier(null));
            Assert.Equal("", CSharpIdentifierHelper.UnsanitizeIdentifier(""));
            Assert.Equal("   ", CSharpIdentifierHelper.UnsanitizeIdentifier("   "));
        }

        /// <summary>
        /// Tests the IsReservedWord method.
        /// </summary>
        [Fact]
        public void IsReservedWord_ShouldCorrectlyIdentifyReservedWords()
        {
            // Reserved words should return true
            Assert.True(CSharpIdentifierHelper.IsReservedWord("out"));
            Assert.True(CSharpIdentifierHelper.IsReservedWord("ref"));
            Assert.True(CSharpIdentifierHelper.IsReservedWord("class"));
            Assert.True(CSharpIdentifierHelper.IsReservedWord("public"));

            // Non-reserved words should return false
            Assert.False(CSharpIdentifierHelper.IsReservedWord("myVariable"));
            Assert.False(CSharpIdentifierHelper.IsReservedWord("test"));
            Assert.False(CSharpIdentifierHelper.IsReservedWord("output"));

            // Edge cases
            Assert.False(CSharpIdentifierHelper.IsReservedWord(null));
            Assert.False(CSharpIdentifierHelper.IsReservedWord(""));
            Assert.False(CSharpIdentifierHelper.IsReservedWord("   "));
        }

        /// <summary>
        /// Tests the IsSanitized method.
        /// </summary>
        [Fact]
        public void IsSanitized_ShouldCorrectlyIdentifySanitizedIdentifiers()
        {
            // Sanitized identifiers should return true
            Assert.True(CSharpIdentifierHelper.IsSanitized("@out"));
            Assert.True(CSharpIdentifierHelper.IsSanitized("@ref"));
            Assert.True(CSharpIdentifierHelper.IsSanitized("@class"));

            // Non-sanitized identifiers should return false
            Assert.False(CSharpIdentifierHelper.IsSanitized("myVariable"));
            Assert.False(CSharpIdentifierHelper.IsSanitized("out"));
            Assert.False(CSharpIdentifierHelper.IsSanitized("@custom")); // @custom is not a reserved word

            // Edge cases
            Assert.False(CSharpIdentifierHelper.IsSanitized(null));
            Assert.False(CSharpIdentifierHelper.IsSanitized(""));
            Assert.False(CSharpIdentifierHelper.IsSanitized("@"));
            Assert.False(CSharpIdentifierHelper.IsSanitized("@   "));
        }

        /// <summary>
        /// Integration test that verifies the complete sanitize/unsanitize cycle.
        /// </summary>
        [Fact]
        public void SanitizeUnsanitizeCycle_ShouldBeReversibleForReservedWords()
        {
            var original = "out";
            var sanitized = CSharpIdentifierHelper.SanitizeIdentifier(original);
            var unsanitized = CSharpIdentifierHelper.UnsanitizeIdentifier(sanitized);

            Assert.Equal("@out", sanitized);
            Assert.Equal(original, unsanitized);
        }

        /// <summary>
        /// Integration test that verifies non-reserved words remain unchanged through the cycle.
        /// </summary>
        [Fact]
        public void SanitizeUnsanitizeCycle_ShouldPreserveNonReservedWords()
        {
            var original = "myVariable";
            var sanitized = CSharpIdentifierHelper.SanitizeIdentifier(original);
            var unsanitized = CSharpIdentifierHelper.UnsanitizeIdentifier(sanitized);

            Assert.Equal(original, sanitized);
            Assert.Equal(original, unsanitized);
        }

        /// <summary>
        /// Runs basic tests to verify sanitization and unsanitization work correctly.
        /// This method can be called during development for quick verification.
        /// </summary>
        public static void RunBasicTests()
        {
            Console.WriteLine("=== CSharpIdentifierHelper Tests ===");

            // Test reserved word sanitization
            var sanitized = CSharpIdentifierHelper.SanitizeIdentifier("out");
            Console.WriteLine($"'out' -> '{sanitized}' (expected: '@out')");
            System.Diagnostics.Debug.Assert(sanitized == "@out");

            var unsanitized = CSharpIdentifierHelper.UnsanitizeIdentifier("@out");
            Console.WriteLine($"'@out' -> '{unsanitized}' (expected: 'out')");
            System.Diagnostics.Debug.Assert(unsanitized == "out");

            // Test non-reserved words (should remain unchanged)
            var normal = CSharpIdentifierHelper.SanitizeIdentifier("myVariable");
            Console.WriteLine($"'myVariable' -> '{normal}' (expected: 'myVariable')");
            System.Diagnostics.Debug.Assert(normal == "myVariable");

            var normalUn = CSharpIdentifierHelper.UnsanitizeIdentifier("myVariable");
            Console.WriteLine($"'myVariable' -> '{normalUn}' (expected: 'myVariable')");
            System.Diagnostics.Debug.Assert(normalUn == "myVariable");

            // Test invalid character handling
            var invalid = CSharpIdentifierHelper.SanitizeIdentifier("my-variable");
            Console.WriteLine($"'my-variable' -> '{invalid}' (expected: 'my_variable')");
            System.Diagnostics.Debug.Assert(invalid == "my_variable");

            // Test digit prefix handling
            var digit = CSharpIdentifierHelper.SanitizeIdentifier("123var");
            Console.WriteLine($"'123var' -> '{digit}' (expected: '_123var')");
            System.Diagnostics.Debug.Assert(digit == "_123var");

            // Test helper methods
            Console.WriteLine($"IsReservedWord('out'): {CSharpIdentifierHelper.IsReservedWord("out")} (expected: True)");
            Console.WriteLine($"IsReservedWord('myVar'): {CSharpIdentifierHelper.IsReservedWord("myVar")} (expected: False)");
            Console.WriteLine($"IsSanitized('@out'): {CSharpIdentifierHelper.IsSanitized("@out")} (expected: True)");
            Console.WriteLine($"IsSanitized('myVar'): {CSharpIdentifierHelper.IsSanitized("myVar")} (expected: False)");

            Console.WriteLine("=== All tests passed! ===");
        }
    }
}
