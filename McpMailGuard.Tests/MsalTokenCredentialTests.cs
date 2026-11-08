// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using Azure.Core;
using McpMailGuard.Accounts;
using McpMailGuard.Providers.Microsoft;
using Microsoft.Identity.Client;

namespace McpMailGuard.Tests;

public class MsalTokenCredentialTests
{
    [Fact]
    public async Task Missing_sign_in_fails_fast_without_prompting()
    {
        // A fresh app with an empty in-memory cache: no accounts, no network needed.
        var app = PublicClientApplicationBuilder.Create(Guid.NewGuid().ToString())
            .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
            .Build();
        var credential = new MsalTokenCredential("hotmail", () => Task.FromResult<IPublicClientApplication>(app), ["Mail.Read"]);

        var ex = await Assert.ThrowsAsync<AccountNotSignedInException>(
            () => credential.GetTokenAsync(new TokenRequestContext(["Mail.Read"]), default).AsTask());
        Assert.Contains("login hotmail", ex.Message);
    }

    [Fact]
    public async Task Empty_account_folder_means_not_signed_in()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mailguard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var client = new MsalAccountClient("hotmail",
            new MicrosoftAccountOptions { ClientId = Guid.NewGuid().ToString() }, dir);

        var app = await client.GetApplicationAsync();
        Assert.Empty(await app.GetAccountsAsync());
        await Assert.ThrowsAsync<AccountNotSignedInException>(
            () => new MsalTokenCredential(client).GetTokenAsync(new TokenRequestContext(["Mail.Read"]), default).AsTask());
    }
}
