// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Abstractions;

/// <summary>
/// Provider-neutral folder aliases accepted by the mail tools.
/// </summary>
public static class MailFolders
{
    public const string Inbox = "inbox";
    public const string Sent = "sent";
    public const string Drafts = "drafts";
    public const string Archive = "archive";
    public const string Deleted = "deleted";
    public const string Junk = "junk";

    /// <summary>
    /// Alias meaning "every folder"; equivalent to passing null.
    /// </summary>
    public const string All = "all";

    public static IReadOnlyList<string> Aliases { get; } = [Inbox, Sent, Drafts, Archive, Deleted, Junk, All];

    /// <summary>
    /// Normalizes a folder argument: trims and lower-cases it, and maps empty or "all" to null.
    /// </summary>
    public static string? Normalize(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        var trimmed = folder.Trim().ToLowerInvariant();
        return trimmed == All ? null : trimmed;
    }
}
