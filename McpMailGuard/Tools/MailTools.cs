// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.ComponentModel;
using McpMailGuard.Abstractions;
using McpMailGuard.Accounts;
using McpMailGuard.Models;
using ModelContextProtocol.Server;

namespace McpMailGuard.Tools;

/// <summary>
/// Read-only mail tools.
/// </summary>
[McpServerToolType]
public sealed class MailTools
{
    private const string AccountDescription =
        "Account name from list_accounts. May be omitted when only one account is configured.";

    private const string FolderDescription =
        "Folder: inbox, sent, drafts, archive, deleted, junk, or all.";

    private readonly AccountRegistry _accounts;

    public MailTools(AccountRegistry accounts) => _accounts = accounts;

    [McpServerTool(Name = "get_recent_emails", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the most recent emails in a folder, newest first. Returns a short preview of each body; use get_email for the full text.")]
    public Task<IReadOnlyList<EmailMessage>> GetRecentEmailsAsync(
        [Description(FolderDescription)] string folder = MailFolders.Inbox,
        [Description("Maximum number of emails to return (1-50).")] int maxResults = 20,
        [Description(AccountDescription)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.RunAsync(() => Mail(account)
            .ListMessagesAsync(folder, ToolGuard.ClampResults(maxResults), cancellationToken));

    [McpServerTool(Name = "search_emails", ReadOnly = true, OpenWorld = false)]
    [Description("Full-text search of emails (subject, body, sender, recipients). Returns a short preview of each match; use get_email for the full text.")]
    public Task<IReadOnlyList<EmailMessage>> SearchEmailsAsync(
        [Description("Words to search for.")] string query,
        [Description(FolderDescription + " Defaults to all.")] string folder = MailFolders.All,
        [Description("Maximum number of emails to return (1-50).")] int maxResults = 20,
        [Description(AccountDescription)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.RunAsync(() => Mail(account)
            .SearchAsync(query, folder, ToolGuard.ClampResults(maxResults), cancellationToken));

    [McpServerTool(Name = "get_email", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one email with its full plain-text body. Very long bodies are truncated.")]
    public Task<EmailMessage?> GetEmailAsync(
        [Description("Email id from get_recent_emails or search_emails.")] string id,
        [Description(AccountDescription)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.RunAsync(() => Mail(account).GetMessageAsync(id, cancellationToken));

    private IEmailProvider Mail(string? account)
    {
        var resolved = _accounts.Resolve(account);
        return resolved.Email
               ?? throw new InvalidOperationException($"Account '{resolved.Name}' does not provide mail.");
    }
}
