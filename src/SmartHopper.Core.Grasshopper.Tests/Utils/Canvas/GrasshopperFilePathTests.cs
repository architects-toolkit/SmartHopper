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
using System.IO;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Canvas
{
    /// <summary>
    /// Tests for <see cref="GrasshopperFilePath"/>.
    /// </summary>
    public sealed class GrasshopperFilePathTests
    {
        /// <summary>
        /// Verifies that empty, relative and non-Grasshopper paths are rejected.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("relative/file.gh")]
        [InlineData("file.gh")]
        public void TryNormalize_RejectsMissingOrRelativePaths(string? path)
        {
            Assert.False(GrasshopperFilePath.TryNormalize(path, mustExist: false, out _, out var error));
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        /// <summary>
        /// Verifies that only .gh and .ghx extensions are accepted, case-insensitively.
        /// </summary>
        [Fact]
        public void TryNormalize_AcceptsOnlyGrasshopperExtensions()
        {
            var folder = Path.GetTempPath();

            Assert.True(GrasshopperFilePath.TryNormalize(Path.Combine(folder, "a.gh"), false, out var gh, out _));
            Assert.True(GrasshopperFilePath.TryNormalize(Path.Combine(folder, "b.GHX"), false, out _, out _));
            Assert.False(GrasshopperFilePath.TryNormalize(Path.Combine(folder, "c.txt"), false, out _, out var error));
            Assert.Contains(".gh", error);
            Assert.Equal(Path.GetFullPath(Path.Combine(folder, "a.gh")), gh);
        }

        /// <summary>
        /// Verifies that open targets must exist.
        /// </summary>
        [Fact]
        public void TryNormalize_RequiresExistingFileWhenMustExist()
        {
            var path = Path.Combine(Path.GetTempPath(), "sh-missing-" + Guid.NewGuid().ToString("N") + ".gh");
            Assert.False(GrasshopperFilePath.TryNormalize(path, mustExist: true, out _, out var error));
            Assert.Contains("not found", error);

            File.WriteAllText(path, string.Empty);
            try
            {
                Assert.True(GrasshopperFilePath.TryNormalize(path, mustExist: true, out _, out _));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
