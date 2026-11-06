// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.ComponentModel;
using McpMailGuard.Abstractions;
using McpMailGuard.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpMailGuard.Tools;

/// <summary>
/// Mail-related MCP tools for agent interaction.
/// </summary>
public class MailTools
{
    private readonly IEmailProvider _emailProvider;
    private readonly ILogger<MailTools> _logger;

    public MailTools(IEmailProvider emailProvider, ILogger<MailTools> logger)
    {
        _emailProvider = emailProvider;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Gets a list of the most recent emails in the mailbox, newest first.")]
    public async Task<IReadOnlyList<EmailMessage>> GetRecentEmailsAsync(
        [Description("Maximum number of emails to return")] int maxResults = 20,
        [Description("Optional folder to read from (e.g. 'inbox', 'sent'); null for all folders")] string? folder = "inbox",
        [Description("Optional full-text search query")] string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching recent emails (max={Max}, folder={Folder}, query={Query})",
            maxResults, folder, searchQuery);
        return await _emailProvider.ListMessagesAsync(maxResults, folder, searchQuery, cancellationToken);
    }

    [McpServerTool]
    [Description("Searches emails for a text query and returns the matches, newest first.")]
    public async Task<IReadOnlyList<EmailMessage>> SearchEmailsAsync(
        [Description("Search query text")] string query,
        [Description("Maximum number of emails to return")] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching emails for query '{Query}' (max={Max})", query, maxResults);
        return await _emailProvider.SearchAsync(query, maxResults, cancellationToken);
    }

    [McpServerTool]
    [Description("Gets the full content of a single email by its id.")]
    public async Task<EmailMessage?> GetEmailAsync(
        [Description("The email id returned by GetRecentEmails or SearchEmails")] string id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting email {Id}", id);
        return await _emailProvider.GetMessageAsync(id, cancellationToken);
    }
}