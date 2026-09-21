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

using SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns.BuiltIn;
using Xunit;

namespace SmartHopper.Infrastructure.Tests.AICall.Sessions
{
    /// <summary>
    /// Tests the tolerant parser behind suggested-prompt chips.
    /// </summary>
    public class SuggestedPromptsParserTests
    {
        [Fact]
        public void Parse_ValidObjectSchema_ReturnsSuggestions()
        {
            var result = SuggestedPromptsParser.Parse("{\"suggestions\": [\"Add a panel\", \"Group these\"]}");

            Assert.Equal(2, result.Count);
            Assert.Equal("Add a panel", result[0]);
            Assert.Equal("Group these", result[1]);
        }

        [Fact]
        public void Parse_BareArray_ReturnsSuggestions()
        {
            var result = SuggestedPromptsParser.Parse("[\"One\", \"Two\"]");

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Parse_EmptyArray_ReturnsEmpty()
        {
            Assert.Empty(SuggestedPromptsParser.Parse("{\"suggestions\": []}"));
            Assert.Empty(SuggestedPromptsParser.Parse("[]"));
        }

        [Fact]
        public void Parse_MalformedJson_ReturnsEmpty()
        {
            Assert.Empty(SuggestedPromptsParser.Parse("not json at all"));
            Assert.Empty(SuggestedPromptsParser.Parse("{suggestions: [}"));
            Assert.Empty(SuggestedPromptsParser.Parse(string.Empty));
            Assert.Empty(SuggestedPromptsParser.Parse(null));
        }

        [Fact]
        public void Parse_MoreThanThree_TruncatesToThree()
        {
            var result = SuggestedPromptsParser.Parse("{\"suggestions\": [\"a\", \"b\", \"c\", \"d\", \"e\"]}");

            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { "a", "b", "c" }, result);
        }

        [Fact]
        public void Parse_SkipsEmptyAndWhitespaceEntries()
        {
            var result = SuggestedPromptsParser.Parse("{\"suggestions\": [\"\", \"  \", \"real\", null, 42]}");

            Assert.Single(result);
            Assert.Equal("real", result[0]);
        }

        [Fact]
        public void Parse_CodeFenced_ReturnsSuggestions()
        {
            var result = SuggestedPromptsParser.Parse("```json\n{\"suggestions\": [\"fenced\"]}\n```");

            Assert.Single(result);
            Assert.Equal("fenced", result[0]);
        }

        [Fact]
        public void Parse_ProseWrapped_ReturnsSuggestions()
        {
            var result = SuggestedPromptsParser.Parse("Here are some ideas: {\"suggestions\": [\"wrapped\"]} hope that helps");

            Assert.Single(result);
            Assert.Equal("wrapped", result[0]);
        }

        [Fact]
        public void Parse_Duplicates_AreRemoved()
        {
            var result = SuggestedPromptsParser.Parse("{\"suggestions\": [\"same\", \"same\", \"other\"]}");

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Parse_LongSuggestion_IsTruncated()
        {
            var longText = new string('x', 300);
            var result = SuggestedPromptsParser.Parse($"{{\"suggestions\": [\"{longText}\"]}}");

            Assert.Single(result);
            Assert.True(result[0].Length <= 140);
        }
    }
}
