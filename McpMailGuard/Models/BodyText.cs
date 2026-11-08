// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// Helpers for body text returned to agents.
/// </summary>
public static class BodyText
{
    public const string TruncationMarker = "\n\n[... truncated by McpMailGuard ...]";

    /// <summary>
    /// Cuts <paramref name="text"/> to at most <paramref name="maxChars"/> characters (plus a marker),
    /// without splitting a surrogate pair. A non-positive limit means no limit.
    /// </summary>
    public static string? Truncate(string? text, int maxChars, out bool truncated)
    {
        truncated = false;
        if (text is null || maxChars <= 0 || text.Length <= maxChars)
            return text;

        var cut = maxChars;
        if (char.IsHighSurrogate(text[cut - 1]))
            cut--;

        truncated = true;
        return string.Concat(text.AsSpan(0, cut), TruncationMarker);
    }
}
