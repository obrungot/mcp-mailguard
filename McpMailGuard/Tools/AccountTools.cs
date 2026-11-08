// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.ComponentModel;
using McpMailGuard.Accounts;
using ModelContextProtocol.Server;

namespace McpMailGuard.Tools;

/// <summary>
/// Describes one configured account to the agent.
/// </summary>
public sealed record AccountInfo(string Name, string Provider, bool HasMail, bool HasCalendar);

/// <summary>
/// Tools about the configured accounts.
/// </summary>
[McpServerToolType]
public sealed class AccountTools
{
    private readonly AccountRegistry _accounts;

    public AccountTools(AccountRegistry accounts) => _accounts = accounts;

    [McpServerTool(Name = "list_accounts", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the mail/calendar accounts this server can read. Pass a name as 'account' to the other tools.")]
    public IReadOnlyList<AccountInfo> ListAccounts() =>
        _accounts.Accounts
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AccountInfo(a.Name, a.ProviderType, a.Email is not null, a.Calendar is not null))
            .ToList();
}
