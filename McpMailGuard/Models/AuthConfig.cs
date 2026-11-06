// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// Authentication modes supported by the Microsoft Graph provider.
/// </summary>
public enum GraphAuthMode
{
    /// <summary>
    /// App-only (client-credentials). Client-credentials. Requires an ORGANIZATIONAL tenant,
    /// admin-consented APPLICATION permissions, and a target <see cref="GraphAuthConfig.UserId"/>.
    /// Does NOT work for personal Microsoft accounts and cannot use "/me".
    /// </summary>
    ClientCredentials,

    /// <summary>
    /// Delegated, interactive device-code sign-in. Works with PERSONAL accounts
    /// (Hotmail / Outlook.com) and organizational accounts. Recommended default for personal mail.
    /// </summary>
    DeviceCode,

    /// <summary>
    /// Delegated, interactive browser sign-in. Desktop machines only.
    /// </summary>
    InteractiveBrowser
}

/// <summary>
/// Configuration for Microsoft Graph authentication (OAuth 2.0).
/// Populated from appsettings.json or environment variables.
/// </summary>
public class GraphAuthConfig
{
    /// <summary>
    /// Microsoft Entra application (client) ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Microsoft Entra directory (tenant) ID, or a well-known value such as
    /// "consumers" (personal accounts only) or "common" (personal + work/school).
    /// Required for app-only mode; optional for delegated mode (defaults to "common").
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Client secret value. Required for app-only (<see cref="GraphAuthMode.ClientCredentials"/>) mode.
    /// Should NOT be committed to source control - use an environment variable.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The authentication mode to use. When left empty, it is inferred:
    /// a client secret implies <see cref="GraphAuthMode.ClientCredentials"/>;
    /// otherwise <see cref="GraphAuthMode.DeviceCode"/> (suitable for personal accounts).
    /// </summary>
    public string? AuthMode { get; set; }

    /// <summary>
    /// Delegated scopes, used by <see cref="GraphAuthMode.DeviceCode"/> and
    /// <see cref="GraphAuthMode.InteractiveBrowser"/>. App-only mode ignores this and uses the
    /// ".default" scope (the application permissions consented on the app registration).
    /// </summary>
    public string[] Scopes { get; set; } = { "User.Read", "Mail.Read", "Calendars.Read" };

    /// <summary>
    /// App-only mode only: the mailbox to read, as a user id (GUID) or user principal name (email).
    /// App-only tokens have no signed-in user, so "/me" is unavailable and a user MUST be specified.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Enables the persistent MSAL token cache for delegated flows so the user only signs in once.
    /// On Windows this uses DPAPI. On Linux it requires a running secret service; disable it
    /// (or accept re-authentication) in minimal containers.
    /// </summary>
    public bool EnableTokenCache { get; set; } = true;

    /// <summary>
    /// Resolves the effective <see cref="GraphAuthMode"/>, applying inference when unset.
    /// </summary>
    public GraphAuthMode ResolveAuthMode()
    {
        if (!string.IsNullOrWhiteSpace(AuthMode) &&
            Enum.TryParse<GraphAuthMode>(AuthMode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        // Inference: a client secret means app-only; otherwise fall back to delegated device code.
        return string.IsNullOrWhiteSpace(ClientSecret)
            ? GraphAuthMode.DeviceCode
            : GraphAuthMode.ClientCredentials;
    }
}
