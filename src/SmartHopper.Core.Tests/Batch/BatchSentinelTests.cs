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

namespace SmartHopper.Core.Tests.Batch
{
    using SmartHopper.Core.ComponentBase.Batch;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="BatchSentinel"/> protocol helpers.
    /// </summary>
    public class BatchSentinelTests
    {
#if NET7_WINDOWS
        [Theory(DisplayName = "BatchSentinel.Wrap produces expected format [Windows]")]
#else
        [Theory(DisplayName = "BatchSentinel.Wrap produces expected format [Core]")]
#endif
        [InlineData("abc", "##SH_BATCH:abc##")]
        [InlineData("123", "##SH_BATCH:123##")]
        [InlineData("", "##SH_BATCH:##")]
        public void Wrap_ValidId_ReturnsExpectedFormat(string customId, string expected)
        {
            var result = BatchSentinel.Wrap(customId);
            Assert.Equal(expected, result);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "BatchSentinel.Is detects sentinel strings [Windows]")]
#else
        [Theory(DisplayName = "BatchSentinel.Is detects sentinel strings [Core]")]
#endif
        [InlineData("##SH_BATCH:abc##", true)]
        [InlineData("##SH_BATCH:##", true)]
        [InlineData("##SH_BATCH:", true)]
        [InlineData("not-a-sentinel", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void Is_VariousInputs_ReturnsExpected(string value, bool expected)
        {
            Assert.Equal(expected, BatchSentinel.Is(value));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "BatchSentinel.TryExtract extracts customId from sentinel [Windows]")]
#else
        [Theory(DisplayName = "BatchSentinel.TryExtract extracts customId from sentinel [Core]")]
#endif
        [InlineData("##SH_BATCH:abc##", "abc", true)]
        [InlineData("##SH_BATCH:item-42##", "item-42", true)]
        [InlineData("##SH_BATCH:##", null, false)]
        [InlineData("not-a-sentinel", null, false)]
        [InlineData(null, null, false)]
        [InlineData("##SH_BATCH:missing-suffix", null, false)]
        public void TryExtract_VariousInputs_ReturnsExpected(string value, string expectedId, bool expectedSuccess)
        {
            var success = BatchSentinel.TryExtract(value, out var customId);
            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedId, customId);
        }
    }
}
