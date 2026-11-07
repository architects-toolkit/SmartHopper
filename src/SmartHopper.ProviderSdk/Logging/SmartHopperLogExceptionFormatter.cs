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
using System.Collections;
using System.Text;

namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// Formats exceptions for log output, including the message, data dictionary,
    /// stack trace, and all inner exceptions.
    /// </summary>
    public static class SmartHopperLogExceptionFormatter
    {
        /// <summary>
        /// Formats the exception as a multi-line log message with optional context.
        /// </summary>
        /// <param name="exception">The exception to format.</param>
        /// <param name="context">Optional context describing where the exception occurred.</param>
        /// <returns>A formatted exception message.</returns>
        public static string FormatException(Exception exception, string? context = null)
        {
            if (exception == null)
            {
                return string.IsNullOrEmpty(context) ? "No exception provided" : $"{context}: No exception provided";
            }

            var sb = new StringBuilder();
            var prefix = string.IsNullOrEmpty(context) ? string.Empty : context + ": ";
            AppendException(sb, exception, prefix, 0);
            return sb.ToString();
        }

        /// <summary>
        /// Strips file paths and line numbers from a stack trace, keeping only the
        /// fully-qualified frame names, before it can reach disk or leave this machine.
        /// </summary>
        /// <param name="stackTrace">The raw stack trace, may be null.</param>
        /// <returns>The normalized stack trace, or null when the input is empty.</returns>
        public static string? NormalizeStackTrace(string? stackTrace)
        {
            if (string.IsNullOrWhiteSpace(stackTrace))
            {
                return null;
            }

            var builder = new StringBuilder(stackTrace.Length);
            var lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                var paren = line.IndexOf('(');
                var method = paren >= 0 ? line.Substring(0, paren).Trim() : line;

                // Drop the "in <path>:line N" location suffix entirely.
                var inIndex = method.IndexOf(" in ", StringComparison.Ordinal);
                if (inIndex >= 0)
                {
                    method = method.Substring(0, inIndex);
                }

                builder.AppendLine(method);
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendException(StringBuilder sb, Exception exception, string prefix, int depth)
        {
            var indent = new string(' ', depth * 2);

            sb.AppendLine($"{indent}{prefix}{exception.GetType().FullName}: {exception.Message}");

            if (exception.Data != null && exception.Data.Count > 0)
            {
                sb.AppendLine($"{indent}Data:");
                foreach (DictionaryEntry entry in exception.Data)
                {
                    var key = entry.Key?.ToString() ?? "null";
                    var value = entry.Value?.ToString() ?? "null";
                    sb.AppendLine($"{indent}  {key}={value}");
                }
            }

            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                sb.AppendLine($"{indent}StackTrace:");
                var lines = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"{indent}  {line.Trim()}");
                    }
                }
            }

            if (exception.InnerException != null)
            {
                sb.AppendLine($"{indent}Inner exception:");
                AppendException(sb, exception.InnerException, string.Empty, depth + 1);
            }
        }
    }
}
