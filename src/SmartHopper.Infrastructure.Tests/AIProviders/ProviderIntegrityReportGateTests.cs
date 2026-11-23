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

namespace SmartHopper.Infrastructure.Tests
{
    using System.Collections.Generic;
    using SmartHopper.Infrastructure.AIProviders;
    using Xunit;

    /// <summary>
    /// Covers the findings-signature dedup behind the integrity report: an
    /// unchanged findings set is reported once per session, a changed set
    /// reports again, and a clean scan re-arms the gate.
    /// </summary>
    public class ProviderIntegrityReportGateTests
    {
        private static ProviderIntegrityIssue Issue(
            string fileName = "SmartHopper.Providers.Test.dll",
            ProviderIntegrityIssueSeverity severity = ProviderIntegrityIssueSeverity.Error,
            string status = "hash mismatch",
            string detail = "details")
        {
            return new ProviderIntegrityIssue
            {
                FileName = fileName,
                Severity = severity,
                Status = status,
                Detail = detail,
            };
        }

        [Fact(DisplayName = "ShouldReport_FirstFindings_ReturnsTrue")]
        public void ShouldReport_FirstFindings_ReturnsTrue()
        {
            var gate = new ProviderIntegrityReportGate();

            Assert.True(gate.ShouldReport(new[] { Issue() }));
        }

        [Fact(DisplayName = "ShouldReport_UnchangedSet_ReturnsFalse")]
        public void ShouldReport_UnchangedSet_ReturnsFalse()
        {
            var gate = new ProviderIntegrityReportGate();
            var issues = new[] { Issue(), Issue("SmartHopper.Providers.Other.dll") };

            Assert.True(gate.ShouldReport(issues));
            Assert.False(gate.ShouldReport(issues));
        }

        [Fact(DisplayName = "ComputeSignature_IsOrderInsensitive")]
        public void ComputeSignature_IsOrderInsensitive()
        {
            var a = Issue("SmartHopper.Providers.A.dll");
            var b = Issue("SmartHopper.Providers.B.dll");

            Assert.Equal(
                ProviderIntegrityReportGate.ComputeSignature(new[] { a, b }),
                ProviderIntegrityReportGate.ComputeSignature(new[] { b, a }));
        }

        [Fact(DisplayName = "ShouldReport_ReorderedSet_ReturnsFalse")]
        public void ShouldReport_ReorderedSet_ReturnsFalse()
        {
            var gate = new ProviderIntegrityReportGate();
            var a = Issue("SmartHopper.Providers.A.dll");
            var b = Issue("SmartHopper.Providers.B.dll");

            Assert.True(gate.ShouldReport(new[] { a, b }));
            Assert.False(gate.ShouldReport(new[] { b, a }));
        }

        [Fact(DisplayName = "ShouldReport_ChangedFile_ReturnsTrue")]
        public void ShouldReport_ChangedFile_ReturnsTrue()
        {
            var gate = new ProviderIntegrityReportGate();

            Assert.True(gate.ShouldReport(new[] { Issue("SmartHopper.Providers.A.dll") }));
            Assert.True(gate.ShouldReport(new[] { Issue("SmartHopper.Providers.B.dll") }));
        }

        [Fact(DisplayName = "ShouldReport_ChangedSeverity_ReturnsTrue")]
        public void ShouldReport_ChangedSeverity_ReturnsTrue()
        {
            var gate = new ProviderIntegrityReportGate();

            Assert.True(gate.ShouldReport(new[] { Issue(severity: ProviderIntegrityIssueSeverity.Error) }));
            Assert.True(gate.ShouldReport(new[] { Issue(severity: ProviderIntegrityIssueSeverity.Warning) }));
        }

        [Fact(DisplayName = "ShouldReport_ChangedStatus_ReturnsTrue")]
        public void ShouldReport_ChangedStatus_ReturnsTrue()
        {
            var gate = new ProviderIntegrityReportGate();

            Assert.True(gate.ShouldReport(new[] { Issue(status: "hash mismatch") }));
            Assert.True(gate.ShouldReport(new[] { Issue(status: "unsigned") }));
        }

        [Fact(DisplayName = "ShouldReport_OnlyDetailDiffers_ReturnsFalse")]
        public void ShouldReport_OnlyDetailDiffers_ReturnsFalse()
        {
            var gate = new ProviderIntegrityReportGate();

            Assert.True(gate.ShouldReport(new[] { Issue(detail: "first detail") }));
            Assert.False(gate.ShouldReport(new[] { Issue(detail: "rewritten detail") }));
        }

        [Fact(DisplayName = "ShouldReport_EmptySet_ReturnsFalseAndRearms")]
        public void ShouldReport_EmptySet_ReturnsFalseAndRearms()
        {
            var gate = new ProviderIntegrityReportGate();
            var issues = new[] { Issue() };

            Assert.True(gate.ShouldReport(issues));
            Assert.False(gate.ShouldReport(new List<ProviderIntegrityIssue>()));
            Assert.False(gate.ShouldReport((IEnumerable<ProviderIntegrityIssue>?)null));

            // A clean scan resets the gate so a later recurrence still reports.
            Assert.True(gate.ShouldReport(issues));
        }

        [Fact(DisplayName = "ComputeSignature_NullAndEmpty_ReturnsEmpty")]
        public void ComputeSignature_NullAndEmpty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, ProviderIntegrityReportGate.ComputeSignature(null));
            Assert.Equal(string.Empty, ProviderIntegrityReportGate.ComputeSignature(new List<ProviderIntegrityIssue>()));
        }
    }
}
