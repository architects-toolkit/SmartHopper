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

namespace SmartHopper.Core.Tests.DataTree
{
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.DataTree;
    using SmartHopper.ProviderSdk.AICall.Core;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="CanonicalValues"/> — the Grasshopper-free
    /// canonicalization used by <see cref="SemanticFingerprint"/>
    /// (<see cref="CanonicalValues.OfValue"/>,
    /// <see cref="CanonicalValues.CanonicalParameters"/> and
    /// <see cref="CanonicalValues.Canonicalize"/>). Goo-level fingerprinting
    /// (<c>Of</c>/<c>OfInputs</c>) requires Grasshopper assemblies and is verified
    /// through the test components instead.
    /// </summary>
    public class CanonicalValuesTests
    {
        [Fact(DisplayName = "Null values fingerprint to a defined token (D21)")]
        public void Null_Value_Produces_NullToken()
        {
            Assert.Equal(CanonicalValues.NullToken, CanonicalValues.OfValue(null, strict: true));
            Assert.Equal(CanonicalValues.NullToken, CanonicalValues.OfValue(null, strict: false));
        }

        [Fact(DisplayName = "Primitive values are deterministic and type-sensitive")]
        public void Primitives_Are_Deterministic()
        {
            Assert.Equal(CanonicalValues.OfValue(42, true), CanonicalValues.OfValue(42, true));
            Assert.Equal(CanonicalValues.OfValue("abc", true), CanonicalValues.OfValue("abc", true));
            Assert.Equal(CanonicalValues.OfValue(2.5, true), CanonicalValues.OfValue(2.5, true));
            Assert.NotEqual(CanonicalValues.OfValue(42, true), CanonicalValues.OfValue("42", true));
            Assert.NotEqual(CanonicalValues.OfValue(1.0, true), CanonicalValues.OfValue(1.0f, true));
        }

        [Fact(DisplayName = "Identical request parameters produce identical fingerprints")]
        public void AIRequestParameters_Identical_SameFingerprint()
        {
            var a = AIRequestParameters.Create().WithModel("gpt-4").WithTemperature(0.5).Build();
            var b = AIRequestParameters.Create().WithModel("gpt-4").WithTemperature(0.5).Build();
            Assert.Equal(CanonicalValues.OfValue(a, true), CanonicalValues.OfValue(b, true));
        }

        [Fact(DisplayName = "Different Extras values produce different fingerprints (D33)")]
        public void AIRequestParameters_Extras_DifferenceDetected()
        {
            var low = AIRequestParameters.Create().WithExtra("reasoning_effort", "low").Build();
            var high = AIRequestParameters.Create().WithExtra("reasoning_effort", "high").Build();
            Assert.NotEqual(CanonicalValues.OfValue(low, true), CanonicalValues.OfValue(high, true));
        }

        [Fact(DisplayName = "Extras key order does not change the fingerprint")]
        public void AIRequestParameters_Extras_OrderInsensitive()
        {
            var a = AIRequestParameters.Create()
                .WithExtra("alpha", "1")
                .WithExtra("beta", "2")
                .Build();
            var b = AIRequestParameters.Create()
                .WithExtra("beta", "2")
                .WithExtra("alpha", "1")
                .Build();
            Assert.Equal(CanonicalValues.OfValue(a, true), CanonicalValues.OfValue(b, true));
        }

        [Fact(DisplayName = "executionFieldsOnly drops transport/policy fields")]
        public void CanonicalParameters_ExecutionOnly_DropsTransportFields()
        {
            var p = AIRequestParameters.Create()
                .WithModel("m")
                .WithTimeout(30)
                .WithBatchTier(true)
                .WithShareIdenticalResults(false)
                .Build();

            var full = CanonicalValues.CanonicalParameters(p, executionFieldsOnly: false);
            var exec = CanonicalValues.CanonicalParameters(p, executionFieldsOnly: true);

            Assert.Contains("timeout", full);
            Assert.Contains("batchTier", full);
            Assert.Contains("shareIdentical", full);
            Assert.DoesNotContain("timeout", exec);
            Assert.DoesNotContain("batchTier", exec);
            Assert.DoesNotContain("shareIdentical", exec);
            Assert.Contains("model", exec);
        }

        [Fact(DisplayName = "Canonicalize sorts object keys but preserves array order")]
        public void Canonicalize_SortsKeys_PreservesArrays()
        {
            var obj = JObject.Parse("{\"b\":2,\"a\":1}");
            Assert.Equal("{\"a\":1,\"b\":2}", CanonicalValues.Canonicalize(obj));

            var arr = JArray.Parse("[3,1,2]");
            Assert.Equal("[3,1,2]", CanonicalValues.Canonicalize(arr));
        }

        [Fact(DisplayName = "Strict mode gives opaque values a unique token; stable mode stays display-stable")]
        public void OpaqueValues_StrictVsStable()
        {
            var opaque = new object();
            var strictA = CanonicalValues.OfValue(opaque, strict: true);
            var strictB = CanonicalValues.OfValue(new object(), strict: true);
            Assert.NotEqual(strictA, strictB);
            Assert.StartsWith("opq:", strictA);

            // Stable mode is deterministic per display string (same as legacy behavior)
            var stable1 = CanonicalValues.OfValue(opaque, strict: false);
            var stable2 = CanonicalValues.OfValue(opaque, strict: false);
            Assert.Equal(stable1, stable2);
        }

        [Fact(DisplayName = "Strict mode never trusts a custom ToString() as content identity")]
        public void CustomToString_Strict_Opaque()
        {
            // Two distinct objects printing the same description must NOT share a
            // strict fingerprint — false equality would silently reuse wrong results.
            var a = new DescribedValue("same description", 1);
            var b = new DescribedValue("same description", 2);

            var strictA = CanonicalValues.OfValue(a, strict: true);
            var strictB = CanonicalValues.OfValue(b, strict: true);

            Assert.StartsWith("opq:", strictA);
            Assert.StartsWith("opq:", strictB);
            Assert.NotEqual(strictA, strictB);
        }

        [Fact(DisplayName = "Strict mode re-mutates identity for the same instance (conservative)")]
        public void CustomToString_StableMode_DisplayToken()
        {
            // Stable mode keeps the display token so the input-change gate stays
            // deterministic; strict mode is the one that guards reuse.
            var v = new DescribedValue("label", 7);
            Assert.StartsWith("t:", CanonicalValues.OfValue(v, strict: false));
        }

        /// <summary>Custom type whose ToString does not reflect its content.</summary>
        private sealed class DescribedValue
        {
            private readonly string label;
            private readonly int content;

            public DescribedValue(string label, int content)
            {
                this.label = label;
                this.content = content;
            }

            public override string ToString() => this.label;
        }
    }
}
