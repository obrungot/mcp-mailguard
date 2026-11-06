// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Models;

namespace McpMailGuard.Abstractions;

/// <summary>
/// Abstraction over an email data source (Outlook/Microsoft Graph, Gmail, IMAP, ...).
/// Implementations are interchangeable and can be selected at runtime via configuration.
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Human-readable provider name used for diagnostics and logging.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns the list of messages in the inbox (most recent first), optionally filtered.
    /// </summary>
    Task<IReadOnlyList<EmailMessage>> ListMessagesAsync(
        int maxResults = 20,
        string? folder = null,
        string? searchQuery = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single message by its provider-specific id.
    /// </summary>
    Task<EmailMessage?> GetMessageAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches messages across the account.
    /// </summary>
    Task<IReadOnlyList<EmailMessage>> SearchAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}