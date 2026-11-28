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

namespace SmartHopper.Infrastructure.Tests.Diagnostics
{
    using SmartHopper.Infrastructure.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="SmartHopperDiagnostics.FilterToReportableEntries"/>.
    /// </summary>
    public sealed class SmartHopperDiagnosticsFilterTests
    {
        private const string TraceLine = "2026-11-12T08:44:47.7211412+00:00 [Trace][Cat] trace noise";
        private const string InfoLine = "2026-11-12T08:44:47.7952299+00:00 [Info][Cat] info entry";
        private const string WarningLine = "2026-11-12T08:44:48.0000000+00:00 [Warning][Cat] warn entry";
        private const string ErrorLine = "2026-11-12T08:44:49.0000000+00:00 [Error][Cat] error entry";

        [Fact]
        public void DropsTraceEntries_KeepsInfoAndAbove()
        {
            var input = string.Join('\n', TraceLine, InfoLine, WarningLine, ErrorLine, TraceLine);

            var result = SmartHopperDiagnostics.FilterToReportableEntries(input);

            Assert.Equal(string.Join('\n', InfoLine, WarningLine, ErrorLine), result);
        }

        [Fact]
        public void KeepsContinuationLines_UnderKeptEntriesOnly()
        {
            var input = string.Join('\n', TraceLine, "   at Foo.Bar()", ErrorLine, "   at Baz.Qux()", "   --- inner ---");

            var result = SmartHopperDiagnostics.FilterToReportableEntries(input);

            Assert.Equal(string.Join('\n', ErrorLine, "   at Baz.Qux()", "   --- inner ---"), result);
        }

        [Fact]
        public void KeepsLeadingOrphanFragment_FromTruncatedTail()
        {
            var input = "  at Some.Truncated.Stack()\n" + ErrorLine;

            var result = SmartHopperDiagnostics.FilterToReportableEntries(input);

            Assert.Equal("  at Some.Truncated.Stack()\n" + ErrorLine, result);
        }

        [Fact]
        public void HandlesCrLfAndTrailingNewline()
        {
            var input = TraceLine + "\r\n" + InfoLine + "\r\n";

            var result = SmartHopperDiagnostics.FilterToReportableEntries(input);

            Assert.Equal(InfoLine, result);
        }

        [Fact]
        public void AllTrace_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, SmartHopperDiagnostics.FilterToReportableEntries(TraceLine + "\n" + TraceLine));
        }

        [Fact]
        public void NullOrEmpty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, SmartHopperDiagnostics.FilterToReportableEntries(null));
            Assert.Equal(string.Empty, SmartHopperDiagnostics.FilterToReportableEntries(string.Empty));
        }
    }
}
