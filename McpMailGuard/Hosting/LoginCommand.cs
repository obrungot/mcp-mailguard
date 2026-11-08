// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Accounts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpMailGuard.Hosting;

/// <summary>
/// <c>McpMailGuard login [account]</c>: interactive sign-in for one account, run from a terminal
/// (in Docker: <c>docker compose run --rm mailguard login &lt;account&gt;</c>).
/// </summary>
public static class LoginCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        string? accountName,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            var sections = AccountRegistry.GetAccountSections(configuration);
            var section = Pick(sections, accountName);
            var factory = AccountRegistry.FindFactory(section, services.GetServices<IAccountProviderFactory>());
            var store = services.GetRequiredService<AccountStore>();

            await factory.LoginAsync(section.Key, section, store, output, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Sign-in cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"Sign-in failed: {ex.Message}");
            return 1;
        }
    }

    private static IConfigurationSection Pick(IReadOnlyList<IConfigurationSection> sections, string? accountName)
    {
        var names = string.Join(", ", sections.Select(s => s.Key));
        if (sections.Count == 0)
            throw new InvalidOperationException(
                "No accounts are configured. Add one, e.g. Accounts__hotmail__Provider=Microsoft and " +
                "Accounts__hotmail__ClientId=<app id>.");

        if (string.IsNullOrWhiteSpace(accountName))
        {
            return sections.Count == 1
                ? sections[0]
                : throw new InvalidOperationException($"Specify which account to sign in: {names}.");
        }

        return sections.FirstOrDefault(s => string.Equals(s.Key, accountName.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"Unknown account '{accountName}'. Configured accounts: {names}.");
    }
}
