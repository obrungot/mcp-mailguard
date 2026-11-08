// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Models;

namespace McpMailGuard.Abstractions;

/// <summary>
/// Read-only access to one account's mail (Microsoft Graph, later Gmail, ...).
/// </summary>
/// <remarks>
/// Folder arguments use the provider-neutral aliases in <see cref="MailFolders"/>; each provider maps
/// them to its own names (Graph well-known folders, Gmail labels). <c>null</c> means all mail.
/// List and search results carry a short <see cref="EmailMessage.Preview"/> instead of the body;
/// <see cref="GetMessageAsync"/> returns the plain-text body.
/// </remarks>
public interface IEmailProvider
{
    /// <summary>
    /// Human-readable provider name used for diagnostics.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns the most recent messages in a folder, newest first.
    /// </summary>
    Task<IReadOnlyList<EmailMessage>> ListMessagesAsync(
        string? folder,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full-text search, optionally limited to a folder.
    /// </summary>
    Task<IReadOnlyList<EmailMessage>> SearchAsync(
        string query,
        string? folder,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single message, including its plain-text body, or null if it does not exist.
    /// </summary>
    Task<EmailMessage?> GetMessageAsync(
        string id,
        CancellationToken cancellationToken = default);
}
