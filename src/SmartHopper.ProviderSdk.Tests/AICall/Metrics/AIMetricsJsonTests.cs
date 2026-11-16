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

namespace SmartHopper.ProviderSdk.Tests.AICall.Metrics
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIMetricsJson"/> shared metrics serialization,
    /// and the provenance fields on <see cref="AIMetrics"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIMetricsJsonTests
    {
        private static AIMetrics SampleMetrics() => new AIMetrics
        {
            Provider = "test-provider",
            Model = "test-model",
            Quantities = new Dictionary<string, double> { ["text_input/token"] = 10, ["text_output/token"] = 5 },
            EstimatedCosts = new Dictionary<string, decimal> { ["USD"] = 0.01m },
            ReportedCosts = new Dictionary<string, decimal> { ["USD"] = 0.012m },
            FinishReason = "stop",
            CompletionTime = 1.5,
            CompletedAt = new DateTimeOffset(2026, 11, 14, 10, 55, 12, TimeSpan.Zero),
            ExecutionId = "exec-1",
            DataCount = 1,
            IterationsCount = 1,
            Role = "main",
        };

        [Fact(DisplayName = "Round-trip preserves all fields including provenance")]
        public void RoundTrip_PreservesAllFields()
        {
            var json = AIMetricsJson.ToJson(SampleMetrics());
            var parsed = AIMetricsJson.FromJson(json);

            Assert.NotNull(parsed);
            Assert.Equal("test-provider", parsed.Provider);
            Assert.Equal("test-model", parsed.Model);
            Assert.Equal(10, parsed.Quantities["text_input/token"]);
            Assert.Equal(0.01m, parsed.EstimatedCosts["USD"]);
            Assert.Equal(0.012m, parsed.ReportedCosts["USD"]);
            Assert.Equal("stop", parsed.FinishReason);
            Assert.Equal(1.5, parsed.CompletionTime);
            Assert.Equal(new DateTimeOffset(2026, 11, 14, 10, 55, 12, TimeSpan.Zero), parsed.CompletedAt);
            Assert.Equal("exec-1", parsed.ExecutionId);
            Assert.Equal(1, parsed.DataCount);
            Assert.Equal(1, parsed.IterationsCount);
            Assert.Equal("main", parsed.Role);
        }

        [Fact(DisplayName = "completed_at serializes as ISO 8601 UTC")]
        public void ToJson_CompletedAt_Iso8601()
        {
            var json = AIMetricsJson.ToJson(SampleMetrics());
            var value = json["completed_at"]?.Value<string>();
            Assert.NotNull(value);
            Assert.Equal("2026-11-14T10:55:12.0000000Z", value);
        }

        [Fact(DisplayName = "Missing provenance fields round-trip as null (legacy data)")]
        public void FromJson_LegacyEntry_NoTimestamp()
        {
            var parsed = AIMetricsJson.FromJson(JObject.Parse(
                "{\"ai_provider\":\"p\",\"ai_model\":\"m\",\"quantities\":{},\"costs\":{\"estimated\":{},\"reported\":{}},\"finish_reason\":\"stop\",\"completion_time\":1.0}"));

            Assert.NotNull(parsed);
            Assert.Null(parsed.CompletedAt);
            Assert.Null(parsed.ExecutionId);
        }

        [Fact(DisplayName = "completed_at survives a JObject.Parse round-trip (Date token)")]
        public void FromJson_CompletedAt_DateToken()
        {
            // JObject.Parse converts ISO strings into JTokenType.Date tokens. Read via
            // ToString() the token yields a culture string that drops the UTC offset
            // and sub-second precision — the restored timestamp must stay correct.
            var original = DateTimeOffset.Parse("2026-11-14T13:02:27.2399207Z", null, System.Globalization.DateTimeStyles.RoundtripKind);
            var serialized = AIMetricsJson.ToJson(new AIMetrics { CompletedAt = original });
            var reparsed = JObject.Parse(serialized.ToString(Newtonsoft.Json.Formatting.None));

            Assert.Equal(JTokenType.Date, reparsed["completed_at"]!.Type);

            var parsed = AIMetricsJson.FromJson(reparsed);
            Assert.Equal(original, parsed.CompletedAt);
        }

        [Fact(DisplayName = "reused flag only emitted when requested")]
        public void ToJson_ReusedFlag_Conditional()
        {
            var fresh = AIMetricsJson.ToJson(SampleMetrics(), reused: false);
            var replayed = AIMetricsJson.ToJson(SampleMetrics(), reused: true);

            Assert.Null(fresh["reused"]);
            Assert.True(replayed["reused"]!.Value<bool>());
        }

        [Fact(DisplayName = "dataCount override wins over the metric's own value")]
        public void ToJson_DataCount_Override()
        {
            var json = AIMetricsJson.ToJson(SampleMetrics(), dataCount: 0);
            Assert.Equal(0, json["data_count"]!.Value<int>());
        }

        [Fact(DisplayName = "WithCombined keeps latest CompletedAt and drops ExecutionId")]
        public void WithCombined_Provenance()
        {
            var a = new AIMetrics { CompletedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), ExecutionId = "a" };
            var b = new AIMetrics { CompletedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), ExecutionId = "b" };

            var combined = a.WithCombined(b);
            Assert.Equal(b.CompletedAt, combined.CompletedAt);
            Assert.Null(combined.ExecutionId);

            var combined2 = b.WithCombined(a);
            Assert.Equal(b.CompletedAt, combined2.CompletedAt);
        }

        [Fact(DisplayName = "WithCombined tolerates missing timestamps")]
        public void WithCombined_NullCompletedAt()
        {
            var withTime = new AIMetrics { CompletedAt = DateTimeOffset.UtcNow };
            var without = new AIMetrics();

            Assert.Equal(withTime.CompletedAt, without.WithCombined(withTime).CompletedAt);
            Assert.Equal(withTime.CompletedAt, withTime.WithCombined(without).CompletedAt);
            Assert.Null(without.WithCombined(without).CompletedAt);
        }

        [Fact(DisplayName = "Record copy carries provenance fields")]
        public void RecordCopy_Provenance()
        {
            var original = SampleMetrics();
            var copy = original with { };
            Assert.Equal(original.CompletedAt, copy.CompletedAt);
            Assert.Equal(original.ExecutionId, copy.ExecutionId);
        }

        [Fact(DisplayName = "FromJson returns null for non-object input")]
        public void FromJson_InvalidInput_ReturnsNull()
        {
            Assert.Null(AIMetricsJson.FromJson(new JValue("not-json")));
            Assert.Null(AIMetricsJson.ToJson(null));
        }
    }
}
