// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Providers.Graph;

/// <summary>
/// Describes which mailbox/calendar the Graph client should address.
/// Delegated tokens act as the signed-in user ("/me"); app-only tokens have no user
/// context and must address a specific user ("/users/{id}").
/// </summary>
public sealed class GraphUserScope
{
    public GraphUserScope(bool useMe, string? userId)
    {
        UseMe = useMe;
        UserId = userId;
    }

    /// <summary>
    /// When true, requests target the signed-in user via "/me" (delegated auth).
    /// </summary>
    public bool UseMe { get; }

    /// <summary>
    /// When <see cref="UseMe"/> is false, the user id / UPN to address (app-only auth).
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    /// Validates that the scope is usable, throwing a clear error otherwise.
    /// </summary>
    public void EnsureValid()
    {
        if (!UseMe && string.IsNullOrWhiteSpace(UserId))
        {
            throw new InvalidOperationException(
                "App-only authentication has no signed-in user, so '/me' cannot be used. " +
                "Set 'Graph:UserId' to the target mailbox (UPN or id), or switch to a delegated " +
                "mode such as 'Graph:AuthMode=DeviceCode' (required for personal/Hotmail accounts).");
        }
    }
}
