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

using System.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests the gh_document_save tool metadata without Rhino runtime activation.
    /// </summary>
    public class GhDocumentSaveToolTests
    {
        [Fact]
        public void GetTools_IsMutatingAndNotReadOnly()
        {
            var tool = new gh_document_save().GetTools().Single();

            Assert.Equal("gh_document_save", tool.Name);
            Assert.True(tool.MutatesCanvas);
            Assert.False(tool.Annotations.ReadOnlyHint ?? true);
            Assert.True(tool.Annotations.DestructiveHint ?? false);
        }
    }
}
