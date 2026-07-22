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

namespace SmartHopper.Core.Tests.AIContext
{
    using System;
    using System.Globalization;
    using System.Linq;
    using SmartHopper.Core.AIContext;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="TimeContextProvider"/> class.
    /// </summary>
    public class TimeContextProviderTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "TimeContextProvider ProviderId is 'time' [Windows]")]
#else
        [Fact(DisplayName = "TimeContextProvider ProviderId is 'time' [Core]")]
#endif
        public void ProviderId_IsTime()
        {
            var provider = new TimeContextProvider();
            Assert.Equal("time", provider.ProviderId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "TimeContextProvider GetContext contains expected keys [Windows]")]
#else
        [Fact(DisplayName = "TimeContextProvider GetContext contains expected keys [Core]")]
#endif
        public void GetContext_ContainsExpectedKeys()
        {
            var provider = new TimeContextProvider();
            var context = provider.GetContext();

            Assert.NotNull(context);
            Assert.Contains("local-datetime", context.Keys);
            Assert.Contains("local-timezone", context.Keys);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "TimeContextProvider GetContext datetime is ISO-like [Windows]")]
#else
        [Fact(DisplayName = "TimeContextProvider GetContext datetime is ISO-like [Core]")]
#endif
        public void GetContext_DateTime_IsParseable()
        {
            var provider = new TimeContextProvider();
            var context = provider.GetContext();

            var dateTimeValue = context["local-datetime"];
            Assert.False(string.IsNullOrWhiteSpace(dateTimeValue));

            // Should be in format yyyy-MM-dd HH:mm:ss
            Assert.Equal(19, dateTimeValue.Length);
            Assert.True(DateTime.TryParseExact(
                dateTimeValue,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _),
                $"Expected parseable datetime but got: {dateTimeValue}");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "TimeContextProvider GetContext timezone starts with UTC [Windows]")]
#else
        [Fact(DisplayName = "TimeContextProvider GetContext timezone starts with UTC [Core]")]
#endif
        public void GetContext_TimeZone_StartsWithUtc()
        {
            var provider = new TimeContextProvider();
            var context = provider.GetContext();

            var tzValue = context["local-timezone"];
            Assert.StartsWith("UTC", tzValue);
        }
    }
}
