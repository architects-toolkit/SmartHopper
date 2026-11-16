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
using System.Linq;
using GhJSON.Core;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Parsing
{
    /// <summary>
    /// Verifies generation-only normalization without a Rhino runtime or provider calls.
    /// </summary>
    public sealed class GhJsonGenerationPolicyTests
    {
        /// <summary>
        /// Generated coordinates are removed without changing identity, state, or wiring.
        /// </summary>
        [Fact]
        public void RemovePivots_PreservesDefinitionAndIsIdempotent()
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [
                    { ""name"": ""Panel"", ""id"": 1, ""pivot"": ""0,0"",
                      ""componentState"": { ""extensions"": { ""gh.panel"": { ""text"": ""Example"" } } } },
                    { ""name"": ""Panel"", ""id"": 2, ""pivot"": ""400,-100"" },
                    { ""name"": ""Panel"", ""id"": 3 }
                ],
                ""connections"": [{ ""from"": { ""id"": 1, ""paramIndex"": 0 }, ""to"": { ""id"": 2, ""paramIndex"": 0 } }]
            }");
            var state = document.Components[0].ComponentState;
            var connection = Assert.Single(document.Connections!);

            GhJsonGenerationPolicy.RemovePivots(document);
            GhJsonGenerationPolicy.RemovePivots(document);

            Assert.All(document.Components, component => Assert.Null(component.Pivot));
            Assert.Same(state, document.Components[0].ComponentState);
            Assert.Same(connection, Assert.Single(document.Connections!));
            Assert.Equal(new int?[] { 1, 2, 3 }, new[]
            {
                document.Components[0].Id,
                document.Components[1].Id,
                document.Components[2].Id,
            });
            var json = JObject.Parse(GhJson.ToJson(document));
            Assert.All((JArray)json["components"]!, component => Assert.Null(component["pivot"]));
        }

        /// <summary>
        /// Imported document parsing retains positions; stripping is generation-only.
        /// </summary>
        [Fact]
        public void Import_DoesNotApplyGenerationPolicy()
        {
            var document = GhJson.FromJson(@"{""schema"":""1.0"",""components"":[{""name"":""Panel"",""id"":1,""pivot"":""0,0""}]}");
            var imported = GhJson.FromJson(GhJson.ToJson(document));

            Assert.NotNull(imported.Components[0].Pivot);
            Assert.Equal(0, imported.Components[0].Pivot!.X);
            Assert.Equal(0, imported.Components[0].Pivot!.Y);
        }

        /// <summary>
        /// Invalid caller input fails explicitly.
        /// </summary>
        [Fact]
        public void RemovePivots_NullDocument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GhJsonGenerationPolicy.RemovePivots(null!));
        }

        /// <summary>
        /// Model-supplied identities are stripped: name stays canonical, integer ids
        /// are preserved, and no fixer-generated GUID leaks back into the output.
        /// </summary>
        [Fact]
        public void NormalizeGenerated_StripsModelAndFixerIdentities()
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [
                    { ""name"": ""Number Slider"", ""id"": 1,
                      ""componentGuid"": ""00000000-0000-0000-0000-000000000001"",
                      ""instanceGuid"": ""11111111-1111-1111-1111-111111111111"",
                      ""pivot"": ""10,20"" },
                    { ""name"": ""Panel"", ""id"": 2,
                      ""instanceGuid"": ""22222222-2222-2222-2222-222222222222"" }
                ],
                ""connections"": [{ ""from"": { ""id"": 1, ""paramIndex"": 0 }, ""to"": { ""id"": 2, ""paramIndex"": 0 } }]
            }");

            var result = GhJsonGenerationPolicy.NormalizeGenerated(document);

            Assert.True(result.Success, string.Join(";", result.Errors));
            Assert.All(result.Document.Components, c =>
            {
                Assert.Null(c.ComponentGuid);
                Assert.Null(c.InstanceGuid);
                Assert.Null(c.Pivot);
            });
            Assert.Equal(new int?[] { 1, 2 }, new[] { result.Document.Components[0].Id, result.Document.Components[1].Id });
            var serialized = GhJson.ToJson(result.Document);
            Assert.DoesNotContain("instanceGuid", serialized);
            Assert.DoesNotContain("componentGuid", serialized);
            Assert.True(GhJson.IsValid(serialized));
        }

        /// <summary>
        /// A nameless component recovers its canonical name through the supplied
        /// host resolver; the GUID still never leaks into the output.
        /// </summary>
        [Fact]
        public void NormalizeGenerated_GuidOnlyComponent_ResolvesNameAndStripsGuid()
        {
            var guid = Guid.NewGuid();
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [{ ""componentGuid"": """ + guid + @""", ""id"": 1 }]
            }");

            var result = GhJsonGenerationPolicy.NormalizeGenerated(
                document, g => g == guid ? "Number Slider" : null);

            Assert.True(result.Success, string.Join(";", result.Errors));
            Assert.Equal("Number Slider", result.Document.Components[0].Name);
            Assert.Null(result.Document.Components[0].ComponentGuid);
        }

        /// <summary>
        /// A GUID-only component the host cannot resolve is an error — it must retry
        /// generation instead of emitting an anonymous component.
        /// </summary>
        [Fact]
        public void NormalizeGenerated_UnresolvableGuidOnlyComponent_Errors()
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [{ ""componentGuid"": """ + Guid.NewGuid() + @""" }]
            }");

            var result = GhJsonGenerationPolicy.NormalizeGenerated(document, _ => null);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("componentGuid"));
        }

        /// <summary>
        /// Dangling wires and group members are reported, never silently dropped:
        /// the retry loop gets the precise error instead of a truncated document.
        /// </summary>
        [Fact]
        public void NormalizeGenerated_DanglingReferences_ErrorWithoutDropping()
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [{ ""name"": ""Panel"", ""id"": 1 }],
                ""connections"": [{ ""from"": { ""id"": 1, ""paramIndex"": 0 }, ""to"": { ""id"": 99, ""paramIndex"": 0 } }],
                ""groups"": [{ ""name"": ""g"", ""members"": [1, 42] }]
            }");

            var result = GhJsonGenerationPolicy.NormalizeGenerated(document);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("99"));
            Assert.Contains(result.Errors, e => e.Contains("42"));
            Assert.Single(result.Document.Connections!);
        }

        /// <summary>
        /// Duplicate supplied ids are ambiguous and must not be silently renumbered.
        /// </summary>
        [Fact]
        public void NormalizeGenerated_DuplicateIds_Error()
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [
                    { ""name"": ""Panel"", ""id"": 3 },
                    { ""name"": ""Number Slider"", ""id"": 3 }
                ]
            }");

            var result = GhJsonGenerationPolicy.NormalizeGenerated(document);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
        }

        /// <summary>
        /// Missing ids are assigned deterministically without disturbing supplied
        /// ids; normalization is idempotent.
        /// </summary>
        [Fact]
        public void NormalizeGenerated_AssignsMissingIdsDeterministically()
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [
                    { ""name"": ""Panel"", ""id"": 5 },
                    { ""name"": ""Number Slider"" },
                    { ""name"": ""Panel"" }
                ]
            }");

            var first = GhJsonGenerationPolicy.NormalizeGenerated(document);
            var second = GhJsonGenerationPolicy.NormalizeGenerated(first.Document);

            Assert.True(first.Success, string.Join(";", first.Errors));
            Assert.True(second.Success, string.Join(";", second.Errors));
            Assert.Equal(5, first.Document.Components[0].Id);
            Assert.NotNull(first.Document.Components[1].Id);
            Assert.NotNull(first.Document.Components[2].Id);
            Assert.Equal(3, first.Document.Components.Select(c => c.Id).Distinct().Count());
        }
    }
}
