// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Hosting;

/// <summary>
/// Server-wide settings, bound from the <c>MailGuard</c> configuration section.
/// </summary>
public sealed class MailGuardOptions
{
    public const string SectionName = "MailGuard";

    /// <summary>
    /// Shared secret that MCP clients must send as <c>Authorization: Bearer &lt;key&gt;</c>.
    /// The server refuses to start without one.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Folder that holds per-account sign-in state. Defaults to a folder under the user's
    /// local application data. In Docker this is the <c>/data</c> volume.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Maximum number of characters of an email body or event description returned to the agent.
    /// </summary>
    public int MaxBodyChars { get; set; } = 20_000;

    /// <summary>
    /// Returns <see cref="DataDirectory"/>, or the per-user default when it is not set.
    /// </summary>
    public string ResolveDataDirectory() =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpMailGuard")
            : DataDirectory;
}
