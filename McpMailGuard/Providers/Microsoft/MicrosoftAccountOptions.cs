// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// Settings for a Microsoft account (Outlook.com / Hotmail / Microsoft 365), bound from
/// <c>Accounts:&lt;name&gt;</c> when <c>Provider</c> is <c>Microsoft</c>.
/// </summary>
public sealed class MicrosoftAccountOptions
{
    /// <summary>
    /// Application (client) ID of the Microsoft Entra app registration.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Sign-in endpoint: "consumers" for personal accounts (the default), "organizations" or a
    /// tenant id for work/school accounts, or "common" for an app registration that allows both.
    /// </summary>
    public string TenantId { get; set; } = "consumers";

    /// <summary>
    /// Delegated Microsoft Graph permissions requested at sign-in. All read-only.
    /// </summary>
    public string[] Scopes { get; set; } = ["User.Read", "Mail.Read", "Calendars.Read"];
}
