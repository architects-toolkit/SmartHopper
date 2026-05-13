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

namespace SmartHopper.Core.Grasshopper.Tests.Utils
{
    using System.Collections.Generic;
    using GhJSON.Core.SchemaModels;
    using SmartHopper.Core.Grasshopper.Utils.Canvas;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="ComponentNameAliases"/>.
    /// All logic is pure string substitution on GhJSON schema objects; no Rhino
    /// or Grasshopper runtime is required.
    /// </summary>
    public class ComponentNameAliasesTests
    {
        [Theory]
        [InlineData("csharp", "C# Script")]
        [InlineData("c#", "C# Script")]
        [InlineData("CSharp Script", "C# Script")]
        [InlineData("python", "Python 3 Script")]
        [InlineData("py", "Python 3 Script")]
        [InlineData("ghpython", "Python 3 Script")]
        [InlineData("slider", "Number Slider")]
        [InlineData("numberslider", "Number Slider")]
        [InlineData("cube", "Box")]
        [InlineData("rect", "Rectangle")]
        [InlineData("pt", "Point")]
        [InlineData("ln", "Line")]
        [InlineData("crv", "Curve")]
        [InlineData("xy", "XY Plane")]
        [InlineData("xz", "XZ Plane")]
        [InlineData("yz", "YZ Plane")]
        [InlineData("int", "Integer")]
        [InlineData("bool", "Boolean")]
        [InlineData("string", "Text")]
        [InlineData("filter", "Stream Filter")]
        public void Resolve_KnownAlias_ReturnsCanonicalName(string alias, string expected)
        {
            Assert.Equal(expected, ComponentNameAliases.Resolve(alias));
        }

        [Theory]
        [InlineData("C# Script")]
        [InlineData("Number Slider")]
        [InlineData("Point")]
        [InlineData("Some Custom Component Name")]
        public void Resolve_AlreadyCanonical_ReturnsSameName(string canonical)
        {
            Assert.Equal(canonical, ComponentNameAliases.Resolve(canonical));
        }

        [Theory]
        [InlineData("UnknownThing")]
        [InlineData("MadeUpName")]
        public void Resolve_UnknownName_ReturnsInputUnchanged(string name)
        {
            Assert.Equal(name, ComponentNameAliases.Resolve(name));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Resolve_NullOrWhitespace_ReturnsInputUnchanged(string? name)
        {
            Assert.Equal(name, ComponentNameAliases.Resolve(name!));
        }

        [Fact]
        public void Resolve_AliasIsCaseInsensitive()
        {
            Assert.Equal("C# Script", ComponentNameAliases.Resolve("CSHARP"));
            Assert.Equal("C# Script", ComponentNameAliases.Resolve("CsHarp"));
            Assert.Equal("Number Slider", ComponentNameAliases.Resolve("SLIDER"));
        }

        [Fact]
        public void Resolve_StripsSurroundingWhitespace()
        {
            Assert.Equal("C# Script", ComponentNameAliases.Resolve("  csharp  "));
            Assert.Equal("Number Slider", ComponentNameAliases.Resolve("\tslider\n"));
        }

        [Fact]
        public void Normalize_NullDocument_ReturnsZero()
        {
            Assert.Equal(0, ComponentNameAliases.Normalize(null));
        }

        [Fact]
        public void Normalize_EmptyDocument_ReturnsZero()
        {
            var doc = new GhJsonDocument(
                schema: null,
                metadata: null,
                components: new List<GhJsonComponent>(),
                connections: null,
                groups: null);

            Assert.Equal(0, ComponentNameAliases.Normalize(doc));
        }

        [Fact]
        public void Normalize_RewritesAliasesAndCountsThem()
        {
            var components = new List<GhJsonComponent>
            {
                new GhJsonComponent { Name = "csharp" },
                new GhJsonComponent { Name = "Number Slider" },
                new GhJsonComponent { Name = "py" },
                new GhJsonComponent { Name = "MadeUpThing" },
            };

            var doc = new GhJsonDocument(
                schema: null,
                metadata: null,
                components: components,
                connections: null,
                groups: null);

            var substitutions = ComponentNameAliases.Normalize(doc);

            Assert.Equal(2, substitutions);
            Assert.Equal("C# Script", doc.Components[0].Name);
            Assert.Equal("Number Slider", doc.Components[1].Name);
            Assert.Equal("Python 3 Script", doc.Components[2].Name);
            Assert.Equal("MadeUpThing", doc.Components[3].Name);
        }

        [Fact]
        public void Normalize_SkipsComponentsWithoutName()
        {
            var components = new List<GhJsonComponent>
            {
                new GhJsonComponent { Name = null },
                new GhJsonComponent { Name = string.Empty },
                new GhJsonComponent { Name = "   " },
                new GhJsonComponent { Name = "slider" },
            };

            var doc = new GhJsonDocument(
                schema: null,
                metadata: null,
                components: components,
                connections: null,
                groups: null);

            var substitutions = ComponentNameAliases.Normalize(doc);

            Assert.Equal(1, substitutions);
            Assert.Null(doc.Components[0].Name);
            Assert.Equal(string.Empty, doc.Components[1].Name);
            Assert.Equal("   ", doc.Components[2].Name);
            Assert.Equal("Number Slider", doc.Components[3].Name);
        }
    }
}
