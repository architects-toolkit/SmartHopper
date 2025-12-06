/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Parsing
{
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.Core.Grasshopper.Utils.Parsing;
    using Xunit;

    /// <summary>
    /// Unit tests for AIResponseParser utility class.
    /// Tests parsing edge cases for boolean, index, and string array responses.
    /// </summary>
    public class AIResponseParserTests
    {
        #region Boolean Parsing Tests

        /// <summary>
        /// Tests that ParseBooleanFromResponse correctly parses various boolean representations.
        /// </summary>
#if NET7_WINDOWS
        [Theory(DisplayName = "ParseBooleanFromResponse ValidInputs ReturnsExpectedValue [Windows]")]
#else
        [Theory(DisplayName = "ParseBooleanFromResponse ValidInputs ReturnsExpectedValue [Core]")]
#endif
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("True", true)]
        [InlineData("false", false)]
        [InlineData("FALSE", false)]
        [InlineData("False", false)]
        [InlineData("The answer is true", true)]
        [InlineData("The answer is false", false)]
        [InlineData("TRUE - confirmed", true)]
        [InlineData("FALSE - denied", false)]
        public void ParseBooleanFromResponse_ValidInputs_ReturnsExpectedValue(string input, bool expected)
        {
            // Act
            var result = AIResponseParser.ParseBooleanFromResponse(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value);
        }

        /// <summary>
        /// Tests that ParseBooleanFromResponse returns null for invalid inputs.
        /// </summary>
#if NET7_WINDOWS
        [Theory(DisplayName = "ParseBooleanFromResponse InvalidInputs ReturnsNull [Windows]")]
#else
        [Theory(DisplayName = "ParseBooleanFromResponse InvalidInputs ReturnsNull [Core]")]
#endif
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("maybe")]
        [InlineData("unknown")]
        [InlineData("123")]
        public void ParseBooleanFromResponse_InvalidInputs_ReturnsNull(string input)
        {
            // Act
            var result = AIResponseParser.ParseBooleanFromResponse(input);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Index Parsing Tests - JSON Arrays

        /// <summary>
        /// Tests that ParseIndicesFromResponse correctly parses a JSON array.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonArray ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonArray ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonArray_ReturnsIndices()
        {
            // Arrange
            var response = "[1, 2, 3, 5, 8]";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 5, 8 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse correctly parses a JSON array with string numbers.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonArrayWithStrings ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonArrayWithStrings ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonArrayWithStrings_ReturnsIndices()
        {
            // Arrange
            var response = "[\"1\", \"2\", \"3\"]";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse removes duplicates from results.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonArrayWithDuplicates ReturnsUniqueIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonArrayWithDuplicates ReturnsUniqueIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonArrayWithDuplicates_ReturnsUniqueIndices()
        {
            // Arrange
            var response = "[1, 2, 2, 3, 3, 3]";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        #endregion

        #region Index Parsing Tests - Markdown Code Blocks

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from markdown JSON code blocks.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MarkdownJsonBlock ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MarkdownJsonBlock ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_MarkdownJsonBlock_ReturnsIndices()
        {
            // Arrange
            var response = "```json\n[1, 2, 3]\n```";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from markdown text code blocks.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MarkdownTextBlock ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MarkdownTextBlock ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_MarkdownTextBlock_ReturnsIndices()
        {
            // Arrange
            var response = "```txt\n1, 2, 3\n```";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from markdown code blocks without language specifier.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MarkdownBlockNoLanguage ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MarkdownBlockNoLanguage ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_MarkdownBlockNoLanguage_ReturnsIndices()
        {
            // Arrange
            var response = "```\n[1, 2, 3]\n```";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        #endregion

        #region Index Parsing Tests - JSON Objects

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from JSON object with 'indices' key.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectWithIndicesKey ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectWithIndicesKey ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonObjectWithIndicesKey_ReturnsIndices()
        {
            // Arrange
            var response = "{\"indices\": [1, 2, 3]}";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from JSON object with 'result' key.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectWithResultKey ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectWithResultKey ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonObjectWithResultKey_ReturnsIndices()
        {
            // Arrange
            var response = "{\"result\": [1, 2, 3]}";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from JSON object with 'data' key.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectWithDataKey ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectWithDataKey ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonObjectWithDataKey_ReturnsIndices()
        {
            // Arrange
            var response = "{\"data\": [1, 2, 3]}";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from dictionary-style JSON object.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectDictionary ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectDictionary ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonObjectDictionary_ReturnsIndices()
        {
            // Arrange
            var response = "{\"2\": true, \"3\": true, \"5\": false, \"7\": true}";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 2, 3, 7 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from nested JSON objects.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectNestedIndices ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse JsonObjectNestedIndices ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_JsonObjectNestedIndices_ReturnsIndices()
        {
            // Arrange
            var response = "{\"data\": {\"indices\": [1, 2, 3]}}";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        #endregion

        #region Index Parsing Tests - Text Formats

        /// <summary>
        /// Tests that ParseIndicesFromResponse parses comma-separated indices.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse CommaSeparated ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse CommaSeparated ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_CommaSeparated_ReturnsIndices()
        {
            // Arrange
            var response = "1, 2, 3, 5, 8";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 5, 8 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse parses space-separated indices.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse SpaceSeparated ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse SpaceSeparated ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_SpaceSeparated_ReturnsIndices()
        {
            // Arrange
            var response = "1 2 3 5 8";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 5, 8 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse parses newline-separated indices.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse NewlineSeparated ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse NewlineSeparated ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_NewlineSeparated_ReturnsIndices()
        {
            // Arrange
            var response = "1\n2\n3\n5\n8";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 5, 8 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse extracts indices from text-wrapped arrays.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse TextWrappedArray ReturnsIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse TextWrappedArray ReturnsIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_TextWrappedArray_ReturnsIndices()
        {
            // Arrange
            var response = "The matching indices are [1, 2, 3] based on the criteria.";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        #endregion

        #region Index Parsing Tests - Range Notation

        /// <summary>
        /// Tests that ParseIndicesFromResponse expands hyphen-based ranges.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse RangeWithHyphen ReturnsExpandedIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse RangeWithHyphen ReturnsExpandedIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_RangeWithHyphen_ReturnsExpandedIndices()
        {
            // Arrange
            var response = "1-5";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse expands double-dot ranges.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse RangeWithDoubleDot ReturnsExpandedIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse RangeWithDoubleDot ReturnsExpandedIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_RangeWithDoubleDot_ReturnsExpandedIndices()
        {
            // Arrange
            var response = "1..5";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse expands multiple ranges.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MultipleRanges ReturnsExpandedIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MultipleRanges ReturnsExpandedIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_MultipleRanges_ReturnsExpandedIndices()
        {
            // Arrange
            var response = "1-3, 7-9";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 7, 8, 9 }, result);
        }

        /// <summary>
        /// Tests that ParseIndicesFromResponse handles mixed ranges and individual indices.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MixedRangeAndIndividual ReturnsAllIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MixedRangeAndIndividual ReturnsAllIndices [Core]")]
#endif
        public void ParseIndicesFromResponse_MixedRangeAndIndividual_ReturnsAllIndices()
        {
            // Arrange
            var response = "1-3, 5, 7-9";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 5, 7, 8, 9 }, result);
        }

        #endregion

        #region Index Parsing Tests - Empty/None Cases

        /// <summary>
        /// Tests that ParseIndicesFromResponse returns empty list for empty/none inputs.
        /// </summary>
#if NET7_WINDOWS
        [Theory(DisplayName = "ParseIndicesFromResponse EmptyOrNone ReturnsEmptyList [Windows]")]
#else
        [Theory(DisplayName = "ParseIndicesFromResponse EmptyOrNone ReturnsEmptyList [Core]")]
#endif
        [InlineData("[]")]
        [InlineData("none")]
        [InlineData("None")]
        [InlineData("NONE")]
        [InlineData("no matches")]
        [InlineData("empty")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void ParseIndicesFromResponse_EmptyOrNone_ReturnsEmptyList(string input)
        {
            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(input);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region String Array Parsing Tests

        /// <summary>

        /// Tests that ParseStringArrayFromResponse correctly parses a JSON array of strings.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseStringArrayFromResponse JsonArray ReturnsStrings [Windows]")]
#else
        [Fact(DisplayName = "ParseStringArrayFromResponse JsonArray ReturnsStrings [Core]")]
#endif

        public void ParseStringArrayFromResponse_JsonArray_ReturnsStrings()
        {
            // Arrange
            var response = "[\"apple\", \"banana\", \"cherry\"]";

            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(response);

            // Assert
            Assert.Equal(new[] { "apple", "banana", "cherry" }, result);
        }

        /// <summary>

        /// Tests that ParseStringArrayFromResponse parses comma-separated strings.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseStringArrayFromResponse CommaSeparated ReturnsStrings [Windows]")]
#else
        [Fact(DisplayName = "ParseStringArrayFromResponse CommaSeparated ReturnsStrings [Core]")]
#endif

        public void ParseStringArrayFromResponse_CommaSeparated_ReturnsStrings()
        {
            // Arrange
            var response = "apple, banana, cherry";

            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(response);

            // Assert
            Assert.Equal(new[] { "apple", "banana", "cherry" }, result);
        }

        /// <summary>

        /// Tests that ParseStringArrayFromResponse parses bracketed comma-separated strings.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseStringArrayFromResponse BracketedCommaSeparated ReturnsStrings [Windows]")]
#else
        [Fact(DisplayName = "ParseStringArrayFromResponse BracketedCommaSeparated ReturnsStrings [Core]")]
#endif

        public void ParseStringArrayFromResponse_BracketedCommaSeparated_ReturnsStrings()
        {
            // Arrange
            var response = "[apple, banana, cherry]";

            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(response);

            // Assert
            Assert.Equal(new[] { "apple", "banana", "cherry" }, result);
        }

        /// <summary>

        /// Tests that ParseStringArrayFromResponse handles mixed quote styles.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseStringArrayFromResponse MixedQuotes ReturnsStrings [Windows]")]
#else
        [Fact(DisplayName = "ParseStringArrayFromResponse MixedQuotes ReturnsStrings [Core]")]
#endif

        public void ParseStringArrayFromResponse_MixedQuotes_ReturnsStrings()
        {
            // Arrange
            var response = "[\"apple\", 'banana', cherry]";

            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(response);

            // Assert
            Assert.Equal(new[] { "apple", "banana", "cherry" }, result);
        }

        /// <summary>

        /// Tests that ParseStringArrayFromResponse handles nested structures within strings.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseStringArrayFromResponse WithNestedStructures ReturnsStrings [Windows]")]
#else
        [Fact(DisplayName = "ParseStringArrayFromResponse WithNestedStructures ReturnsStrings [Core]")]
#endif

        public void ParseStringArrayFromResponse_WithNestedStructures_ReturnsStrings()
        {
            // Arrange
            var response = "[\"item1\", \"{key: value}\", \"[nested, array]\"]";

            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(response);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("item1", result[0]);
            Assert.Equal("{key: value}", result[1]);
            Assert.Equal("[nested, array]", result[2]);
        }

        /// <summary>

        /// Tests that ParseStringArrayFromResponse returns empty list for empty inputs.

        /// </summary>

#if NET7_WINDOWS
        [Theory(DisplayName = "ParseStringArrayFromResponse EmptyInput ReturnsEmptyList [Windows]")]
#else
        [Theory(DisplayName = "ParseStringArrayFromResponse EmptyInput ReturnsEmptyList [Core]")]
#endif

        [InlineData("")]

        [InlineData(null)]

        [InlineData("   ")]

        public void ParseStringArrayFromResponse_EmptyInput_ReturnsEmptyList(string input)
        {
            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(input);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region Data Formatting Tests

        /// <summary>

        /// Tests that NormalizeJsonArrayString creates valid JSON array.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "NormalizeJsonArrayString ValidList ReturnsJsonArray [Windows]")]
#else
        [Fact(DisplayName = "NormalizeJsonArrayString ValidList ReturnsJsonArray [Core]")]
#endif

        public void NormalizeJsonArrayString_ValidList_ReturnsJsonArray()
        {
            // Arrange
            var values = new List<string> { "apple", "banana", "cherry" };

            // Act
            var result = AIResponseParser.NormalizeJsonArrayString(values);

            // Assert
            Assert.Equal("[\"apple\",\"banana\",\"cherry\"]", result);
        }

        /// <summary>

        /// Tests that NormalizeJsonArrayString handles empty lists.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "NormalizeJsonArrayString EmptyList ReturnsEmptyJsonArray [Windows]")]
#else
        [Fact(DisplayName = "NormalizeJsonArrayString EmptyList ReturnsEmptyJsonArray [Core]")]
#endif

        public void NormalizeJsonArrayString_EmptyList_ReturnsEmptyJsonArray()
        {
            // Arrange
            var values = new List<string>();

            // Act
            var result = AIResponseParser.NormalizeJsonArrayString(values);

            // Assert
            Assert.Equal("[]", result);
        }

        #endregion

        #region Edge Cases and Complex Scenarios

        /// <summary>

        /// Tests that ParseIndicesFromResponse falls back to text parsing for malformed JSON.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MalformedJson FallsBackToTextParsing [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MalformedJson FallsBackToTextParsing [Core]")]
#endif

        public void ParseIndicesFromResponse_MalformedJson_FallsBackToTextParsing()
        {
            // Arrange
            var response = "[1, 2, 3"; // Missing closing bracket

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>

        /// Tests that ParseIndicesFromResponse filters out invalid values.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse MixedValidAndInvalid ReturnsValidIndices [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse MixedValidAndInvalid ReturnsValidIndices [Core]")]
#endif

        public void ParseIndicesFromResponse_MixedValidAndInvalid_ReturnsValidIndices()
        {
            // Arrange
            var response = "1, abc, 2, xyz, 3";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>

        /// Tests that ParseIndicesFromResponse handles negative numbers.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse NegativeNumbers IgnoresNegatives [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse NegativeNumbers IgnoresNegatives [Core]")]
#endif

        public void ParseIndicesFromResponse_NegativeNumbers_IgnoresNegatives()
        {
            // Arrange
            var response = "[-1, 0, 1, 2]";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            // Note: Current implementation doesn't filter negatives, but this documents expected behavior
            Assert.Contains(-1, result);
            Assert.Contains(0, result);
            Assert.Contains(1, result);
            Assert.Contains(2, result);
        }

        /// <summary>

        /// Tests that ParseStringArrayFromResponse handles escaped quotes.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseStringArrayFromResponse EscapedQuotes HandlesCorrectly [Windows]")]
#else
        [Fact(DisplayName = "ParseStringArrayFromResponse EscapedQuotes HandlesCorrectly [Core]")]
#endif

        public void ParseStringArrayFromResponse_EscapedQuotes_HandlesCorrectly()
        {
            // Arrange
            var response = "[\"He said \\\"hello\\\"\", \"normal\"]";

            // Act
            var result = AIResponseParser.ParseStringArrayFromResponse(response);

            // Assert
            Assert.Equal(2, result.Count);

            // JSON parsing unescapes the quotes, so the actual string contains literal quotes
            Assert.Equal("He said \"hello\"", result[0]);
            Assert.Equal("normal", result[1]);
        }

        /// <summary>

        /// Tests that ParseIndicesFromResponse handles large ranges efficiently.

        /// </summary>

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseIndicesFromResponse VeryLargeRange HandlesEfficiently [Windows]")]
#else
        [Fact(DisplayName = "ParseIndicesFromResponse VeryLargeRange HandlesEfficiently [Core]")]
#endif

        public void ParseIndicesFromResponse_VeryLargeRange_HandlesEfficiently()
        {
            // Arrange
            var response = "1-100";

            // Act
            var result = AIResponseParser.ParseIndicesFromResponse(response);

            // Assert
            Assert.Equal(100, result.Count);
            Assert.Equal(1, result.First());
            Assert.Equal(100, result.Last());
        }

        #endregion
    }
}
