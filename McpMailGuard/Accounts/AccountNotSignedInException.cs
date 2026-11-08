// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Accounts;

/// <summary>
/// Thrown when an account has no saved sign-in, or its sign-in can no longer be refreshed silently.
/// The server never prompts during a tool call; the user signs in with the <c>login</c> command.
/// </summary>
public sealed class AccountNotSignedInException : InvalidOperationException
{
    public AccountNotSignedInException(string accountName, Exception? innerException = null)
        : base(
            $"Account '{accountName}' is not signed in, or its sign-in has expired. " +
            $"Sign in from a terminal with 'McpMailGuard login {accountName}' " +
            $"(Docker: 'docker compose run --rm mailguard login {accountName}').",
            innerException)
    {
        AccountName = accountName;
    }

    public string AccountName { get; }
}
