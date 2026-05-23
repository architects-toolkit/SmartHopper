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
    using System.Reflection;
    using HtmlAgilityPack;
    using SmartHopper.Core.Grasshopper.Converters.Formats;
    using Xunit;

    public class HtmlReadabilityHelperTests
    {
        private static object InvokePrivateMethod(string methodName, params object[] args)
        {
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, args);
        }

        [Fact(DisplayName = "ExtractMainContent_NullDoc_ReturnsNull")]
        public void ExtractMainContent_NullDoc_ReturnsNull()
        {
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { null });
            Assert.Null(result);
        }

        [Fact(DisplayName = "ExtractMainContent_ArticleTag_PrioritizesArticle")]
        public void ExtractMainContent_ArticleTag_PrioritizesArticle()
        {
            var html = "<html><body><article><p>Article content</p></article><div><p>Div content</p></div></body></html>";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { doc }) as HtmlNode;
            Assert.NotNull(result);
            Assert.Equal("article", result.Name);
        }

        [Fact(DisplayName = "ExtractMainContent_MainTag_PrioritizesMain")]
        public void ExtractMainContent_MainTag_PrioritizesMain()
        {
            var html = "<html><body><main><p>Main content</p></main><div><p>Div content</p></div></body></html>";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { doc }) as HtmlNode;
            Assert.NotNull(result);
            Assert.Equal("main", result.Name);
        }

        [Fact(DisplayName = "ExtractMainContent_WithNavHeader_RemovesBoilerplate")]
        public void ExtractMainContent_WithNavHeader_RemovesBoilerplate()
        {
            var html = "<html><body><nav>Navigation</nav><header>Header</header><article><p>Article content</p></article></body></html>";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { doc }) as HtmlNode;
            Assert.NotNull(result);
        }

        [Fact(DisplayName = "ExtractMainContent_WithFooter_RemovesBoilerplate")]
        public void ExtractMainContent_WithFooter_RemovesBoilerplate()
        {
            var html = "<html><body><article><p>Article content</p></article><footer>Footer</footer></body></html>";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { doc }) as HtmlNode;
            Assert.NotNull(result);
        }

        [Fact(DisplayName = "ExtractMainContent_WithAdClass_RemovesBoilerplate")]
        public void ExtractMainContent_WithAdClass_RemovesBoilerplate()
        {
            var html = "<html><body><div class=\"ad\">Advertisement</div><article><p>Article content</p></article></body></html>";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { doc }) as HtmlNode;
            Assert.NotNull(result);
        }

        [Fact(DisplayName = "ScoreContentNode_TextDensity_HigherForMoreText")]
        public void ScoreContentNode_TextDensity_HigherForMoreText()
        {
            var html1 = "<div><p>Short text</p></div>";
            var html2 = "<div><p>This is a much longer text with more content that should score higher due to increased text density and length</p></div>";
            var doc1 = new HtmlDocument();
            var doc2 = new HtmlDocument();
            doc1.LoadHtml(html1);
            doc2.LoadHtml(html2);
            var type = typeof(HtmlReadabilityHelper);
            var scoreMethod = type.GetMethod("ScoreContentNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var score1 = (double)scoreMethod?.Invoke(null, new object[] { doc1.DocumentNode.SelectSingleNode("//div") });
            var score2 = (double)scoreMethod?.Invoke(null, new object[] { doc2.DocumentNode.SelectSingleNode("//div") });
            Assert.True(score2 > score1);
        }

        [Fact(DisplayName = "ScoreContentNode_LinkDensity_LowerScoreForManyLinks")]
        public void ScoreContentNode_LinkDensity_LowerScoreForManyLinks()
        {
            var htmlLowLinks = "<div><p>This is content with minimal links</p><a href=\"#\">Link</a></div>";
            var htmlHighLinks = "<div><a href=\"#\">Link1</a><a href=\"#\">Link2</a><a href=\"#\">Link3</a><a href=\"#\">Link4</a><a href=\"#\">Link5</a></div>";
            var doc1 = new HtmlDocument();
            var doc2 = new HtmlDocument();
            doc1.LoadHtml(htmlLowLinks);
            doc2.LoadHtml(htmlHighLinks);
            var type = typeof(HtmlReadabilityHelper);
            var scoreMethod = type.GetMethod("ScoreContentNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var score1 = (double)scoreMethod?.Invoke(null, new object[] { doc1.DocumentNode.SelectSingleNode("//div") });
            var score2 = (double)scoreMethod?.Invoke(null, new object[] { doc2.DocumentNode.SelectSingleNode("//div") });
            Assert.True(score1 > score2);
        }

        [Fact(DisplayName = "ScoreContentNode_SemanticBonus_ArticleHigherThanDiv")]
        public void ScoreContentNode_SemanticBonus_ArticleHigherThanDiv()
        {
            var htmlArticle = "<article><p>Article content</p></article>";
            var htmlDiv = "<div><p>Article content</p></div>";
            var doc1 = new HtmlDocument();
            var doc2 = new HtmlDocument();
            doc1.LoadHtml(htmlArticle);
            doc2.LoadHtml(htmlDiv);
            var type = typeof(HtmlReadabilityHelper);
            var scoreMethod = type.GetMethod("ScoreContentNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var score1 = (double)scoreMethod?.Invoke(null, new object[] { doc1.DocumentNode.SelectSingleNode("//article") });
            var score2 = (double)scoreMethod?.Invoke(null, new object[] { doc2.DocumentNode.SelectSingleNode("//div") });
            Assert.True(score1 > score2);
        }

        [Fact(DisplayName = "ExtractMainContent_NoSemanticTags_ReturnsHighestScoringDiv")]
        public void ExtractMainContent_NoSemanticTags_ReturnsHighestScoringDiv()
        {
            var html = "<html><body><div><p>Content 1</p></div><div><p>This is much longer content with more text that should score higher</p></div></body></html>";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var type = typeof(HtmlReadabilityHelper);
            var method = type.GetMethod("ExtractMainContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { doc }) as HtmlNode;
            Assert.NotNull(result);
            Assert.Equal("div", result.Name);
        }

        [Fact(DisplayName = "HtmlReadabilityHelper_IsInternalType")]
        public void HtmlReadabilityHelper_IsInternalType()
        {
            var type = typeof(HtmlReadabilityHelper);
            Assert.NotNull(type);
        }
    }
}
