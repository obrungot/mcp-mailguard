// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using Azure.Core;
using McpMailGuard.Accounts;
using Microsoft.Identity.Client;

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// The credential Microsoft Graph uses while serving: silent token acquisition from the account's
/// cache only. It never prompts; a missing or revoked sign-in becomes
/// <see cref="AccountNotSignedInException"/> immediately.
/// </summary>
internal sealed class MsalTokenCredential : TokenCredential
{
    private readonly string _accountName;
    private readonly Func<Task<IPublicClientApplication>> _getApplication;
    private readonly IReadOnlyList<string> _scopes;

    public MsalTokenCredential(
        string accountName, Func<Task<IPublicClientApplication>> getApplication, IReadOnlyList<string> scopes)
    {
        _accountName = accountName;
        _getApplication = getApplication;
        _scopes = scopes;
    }

    public MsalTokenCredential(MsalAccountClient client)
        : this(client.AccountName, client.GetApplicationAsync, client.Scopes)
    {
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var app = await _getApplication().ConfigureAwait(false);
        var account = (await app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault()
                      ?? throw new AccountNotSignedInException(_accountName);

        var scopes = requestContext.Scopes is { Length: > 0 } requested ? requested : _scopes;
        try
        {
            var result = await app.AcquireTokenSilent(scopes, account)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
        catch (MsalUiRequiredException ex)
        {
            throw new AccountNotSignedInException(_accountName, ex);
        }
    }
}
