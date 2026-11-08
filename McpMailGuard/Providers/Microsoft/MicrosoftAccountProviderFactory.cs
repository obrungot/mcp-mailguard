// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Accounts;
using McpMailGuard.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// Accounts with <c>Provider = Microsoft</c>: Outlook.com / Hotmail and Microsoft 365 via Microsoft Graph,
/// using delegated device-code sign-in.
/// </summary>
public sealed class MicrosoftAccountProviderFactory : IAccountProviderFactory
{
    public string ProviderType => "Microsoft";

    public MailAccount Create(
        string accountName, IConfigurationSection section, AccountStore store, IServiceProvider services)
    {
        var options = ReadOptions(accountName, section);
        var accountDirectory = store.GetAccountDirectory(accountName);
        var maxBodyChars = services.GetRequiredService<IOptions<MailGuardOptions>>().Value.MaxBodyChars;
        var loggers = services.GetRequiredService<ILoggerFactory>();

        var msal = new MsalAccountClient(accountName, options, accountDirectory);
        var client = new GraphServiceClient(new MsalTokenCredential(msal), options.Scopes);

        return new MailAccount(
            accountName,
            ProviderType,
            new GraphEmailProvider(client, maxBodyChars, loggers.CreateLogger<GraphEmailProvider>()),
            new GraphCalendarProvider(client, maxBodyChars, loggers.CreateLogger<GraphCalendarProvider>()));
    }

    public async Task LoginAsync(
        string accountName,
        IConfigurationSection section,
        AccountStore store,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var options = ReadOptions(accountName, section);
        var accountDirectory = store.GetAccountDirectory(accountName);

        await output.WriteLineAsync(
            $"Signing in account '{accountName}' (Microsoft, endpoint '{options.TenantId}').");
        await output.WriteLineAsync("Open the link below on any device and enter the code.");

        var msal = new MsalAccountClient(accountName, options, accountDirectory);
        try
        {
            var result = await msal.SignInWithDeviceCodeAsync(output, cancellationToken);

            // Keep only the account just signed in, so the server never picks up a previous one.
            var app = await msal.GetApplicationAsync();
            foreach (var other in await app.GetAccountsAsync())
            {
                if (other.HomeAccountId?.Identifier != result.Account.HomeAccountId?.Identifier)
                    await app.RemoveAsync(other);
            }

            // Verify through the same silent path the server uses, so a broken cache shows up now.
            var client = new GraphServiceClient(new MsalTokenCredential(msal), options.Scopes);
            var me = await client.Me.GetAsync(
                rc => rc.QueryParameters.Select = ["displayName", "mail", "userPrincipalName"], cancellationToken);

            var address = me?.Mail ?? me?.UserPrincipalName ?? result.Account.Username;
            await output.WriteLineAsync($"Signed in as {me?.DisplayName} <{address}>. The sign-in is saved; " +
                                        "the server will use it without prompting.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw GraphErrors.Describe(ex);
        }
    }

    private static MicrosoftAccountOptions ReadOptions(string accountName, IConfigurationSection section)
    {
        var options = section.Get<MicrosoftAccountOptions>() ?? new MicrosoftAccountOptions();
        if (string.IsNullOrWhiteSpace(options.ClientId))
            throw new InvalidOperationException(
                $"Account '{accountName}' needs 'ClientId' (the Entra app registration's application id), " +
                $"e.g. environment variable Accounts__{accountName}__ClientId.");

        if (options.Scopes is not { Length: > 0 })
            options.Scopes = new MicrosoftAccountOptions().Scopes;

        return options;
    }
}
