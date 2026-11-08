// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.Globalization;
using McpMailGuard.Abstractions;

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// Pure helpers for building Microsoft Graph queries.
/// </summary>
public static class GraphQuery
{
    private static readonly Dictionary<string, string> WellKnownFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        [MailFolders.Inbox] = "inbox",
        [MailFolders.Sent] = "sentitems",
        ["sentitems"] = "sentitems",
        [MailFolders.Drafts] = "drafts",
        [MailFolders.Archive] = "archive",
        [MailFolders.Deleted] = "deleteditems",
        ["deleteditems"] = "deleteditems",
        [MailFolders.Junk] = "junkemail",
        ["junkemail"] = "junkemail"
    };

    /// <summary>
    /// Maps a folder alias to a Graph well-known folder name. Null/"all" means every folder (null);
    /// anything else is passed through as a Graph folder id.
    /// </summary>
    public static string? ResolveFolder(string? folder)
    {
        var normalized = MailFolders.Normalize(folder);
        if (normalized is null)
            return null;

        return WellKnownFolders.TryGetValue(normalized, out var wellKnown) ? wellKnown : folder!.Trim();
    }

    /// <summary>
    /// Wraps free text as a quoted <c>$search</c> value, escaping backslashes and double quotes.
    /// </summary>
    public static string QuoteSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("A search query is required.", nameof(query));

        var escaped = query.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Formats a time as the UTC ISO-8601 string Graph expects for calendarView windows.
    /// </summary>
    public static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a Graph date/time string (returned in UTC because requests send
    /// <c>Prefer: outlook.timezone="UTC"</c>).
    /// </summary>
    public static DateTimeOffset? ParseUtc(string? value) =>
        value is not null && DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
