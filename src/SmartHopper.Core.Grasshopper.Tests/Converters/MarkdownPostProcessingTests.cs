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

namespace SmartHopper.Core.Grasshopper.Tests.Converters
{
    using SmartHopper.Core.Grasshopper.Converters;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="MarkdownStyleCleanup"/> and <see cref="MarkdownListRenumberer"/>.
    /// </summary>
    public class MarkdownPostProcessingTests
    {
        [Fact]
        public void Cleanup_RemovesBlankLineBeforeNestedOrderedListStart()
        {
            const string input =
                "1. `h1` Beetles\n" +
                "   1. `h2` Etymology\n" +
                "   2. `h2` Distribution and Diversity\n" +
                "   3. `h2` Evolution\n" +
                "\n" +
                "      1. `h3` Late Paleozoic\n" +
                "      2. `h3` Jurassic\n";

            const string expected =
                "1. `h1` Beetles\n" +
                "   1. `h2` Etymology\n" +
                "   2. `h2` Distribution and Diversity\n" +
                "   3. `h2` Evolution\n" +
                "      1. `h3` Late Paleozoic\n" +
                "      2. `h3` Jurassic";

            string actual = MarkdownStyleCleanup.Cleanup(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Cleanup_KeepsBlankLineBetweenParagraphAndNestedList()
        {
            const string input =
                "1. Item\n" +
                "   Paragraph in item.\n" +
                "\n" +
                "   1. Nested item\n";

            string actual = MarkdownStyleCleanup.Cleanup(input);
            Assert.Contains("Paragraph in item.", actual);
            Assert.Contains("   1. Nested item", actual);
        }

        [Fact]
        public void Cleanup_KeepsBlankLineBetweenTopLevelLists()
        {
            const string input =
                "1. First item\n" +
                "\n" +
                "1. New list starts here\n";

            string actual = MarkdownStyleCleanup.Cleanup(input);
            Assert.Contains("1. First item", actual);
            Assert.Contains("1. New list starts here", actual);
        }

        [Fact]
        public void Renumberer_RenumberesConsecutiveOrderedItems()
        {
            const string input =
                "1. A\n" +
                "1. B\n" +
                "1. C\n";

            string actual = MarkdownListRenumberer.Renumber(input);
            Assert.Equal("1. A\n2. B\n3. C\n", actual);
        }
    }
}
