/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Centralized utility for handling C# identifier sanitization and unsanitization.
    /// Provides consistent behavior across GhJSON extraction and placement operations.
    /// </summary>
    public static class CSharpIdentifierHelper
    {
        /// <summary>
        /// Minimal set of C# reserved words that are commonly encountered in Grasshopper scripts.
        /// This covers the most frequent issues while keeping the list manageable.
        /// </summary>
        private static readonly HashSet<string> ReservedWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "out", "ref", "params", "class", "namespace", "object", "string", "int", "float", "double",
            "public", "private", "protected", "internal", "static", "void", "var", "new"
        };

        /// <summary>
        /// Sanitizes a C# identifier by escaping reserved words or invalid identifiers with '@',
        /// and replacing invalid characters with underscores.
        /// </summary>
        /// <param name="identifier">The original identifier to sanitize</param>
        /// <returns>The sanitized identifier safe for use in C# code</returns>
        public static string SanitizeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return identifier;

            // Replace spaces and invalid chars with '_'
            var sanitized = new string(identifier.Select(ch =>
                char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());

            // If starts with digit, prefix underscore
            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;

            // Escape reserved words with @ prefix
            if (ReservedWords.Contains(sanitized))
                sanitized = "@" + sanitized;

            return sanitized;
        }

        /// <summary>
        /// Unsanitizes a C# identifier by removing the @ prefix from reserved keywords.
        /// This reverses the sanitization done during component placement.
        /// </summary>
        /// <param name="identifier">The potentially sanitized identifier</param>
        /// <returns>The original unsanitized identifier</returns>
        public static string UnsanitizeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return identifier;

            // If identifier starts with @ and the rest is a reserved word, remove the @
            if (identifier.StartsWith("@", StringComparison.Ordinal) && identifier.Length > 1)
            {
                var withoutAt = identifier.Substring(1);
                if (ReservedWords.Contains(withoutAt))
                    return withoutAt;
            }

            return identifier;
        }

        /// <summary>
        /// Checks if an identifier is a C# reserved word.
        /// </summary>
        /// <param name="identifier">The identifier to check</param>
        /// <returns>True if the identifier is a reserved word</returns>
        public static bool IsReservedWord(string identifier)
        {
            return !string.IsNullOrWhiteSpace(identifier) && ReservedWords.Contains(identifier);
        }

        /// <summary>
        /// Checks if an identifier appears to be sanitized (starts with @ and is a reserved word).
        /// </summary>
        /// <param name="identifier">The identifier to check</param>
        /// <returns>True if the identifier appears to be sanitized</returns>
        public static bool IsSanitized(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            return identifier.StartsWith("@", StringComparison.Ordinal) &&
                   identifier.Length > 1 &&
                   ReservedWords.Contains(identifier.Substring(1));
        }
    }
}
