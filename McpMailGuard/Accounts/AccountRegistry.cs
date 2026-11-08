// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using Microsoft.Extensions.Configuration;

namespace McpMailGuard.Accounts;

/// <summary>
/// Holds every account configured under <c>Accounts:&lt;name&gt;</c> and resolves the account a
/// tool call refers to.
/// </summary>
public sealed class AccountRegistry
{
    public const string SectionName = "Accounts";

    private readonly Dictionary<string, MailAccount> _accounts;

    public AccountRegistry(
        IConfiguration configuration,
        IEnumerable<IAccountProviderFactory> factories,
        AccountStore store,
        IServiceProvider services)
    {
        var factoryList = factories.ToList();
        _accounts = new Dictionary<string, MailAccount>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in GetAccountSections(configuration))
        {
            var factory = FindFactory(section, factoryList);
            _accounts[section.Key] = factory.Create(section.Key, section, store, services);
        }
    }

    public IReadOnlyCollection<MailAccount> Accounts => _accounts.Values;

    /// <summary>
    /// Returns the named account, or the only account when <paramref name="accountName"/> is empty
    /// and exactly one is configured.
    /// </summary>
    public MailAccount Resolve(string? accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return _accounts.Count switch
            {
                1 => _accounts.Values.First(),
                0 => throw new InvalidOperationException("No accounts are configured on this server."),
                _ => throw new InvalidOperationException(
                    $"Several accounts are configured; pass 'account' as one of: {Names()}.")
            };
        }

        return _accounts.TryGetValue(accountName.Trim(), out var account)
            ? account
            : throw new InvalidOperationException($"Unknown account '{accountName}'. Configured accounts: {Names()}.");
    }

    /// <summary>
    /// The configuration sections of all accounts, i.e. the children of <c>Accounts</c>.
    /// </summary>
    public static IReadOnlyList<IConfigurationSection> GetAccountSections(IConfiguration configuration) =>
        configuration.GetSection(SectionName).GetChildren()
            .Where(s => s.GetChildren().Any())
            .ToList();

    /// <summary>
    /// Finds the factory for an account section's <c>Provider</c> setting.
    /// </summary>
    public static IAccountProviderFactory FindFactory(
        IConfigurationSection accountSection, IEnumerable<IAccountProviderFactory> factories)
    {
        AccountStore.ValidateName(accountSection.Key);

        var factoryList = factories.ToList();
        var provider = accountSection["Provider"];
        if (string.IsNullOrWhiteSpace(provider))
            throw new InvalidOperationException(
                $"Account '{accountSection.Key}' has no 'Provider' setting. Supported: {Supported(factoryList)}.");

        return factoryList.FirstOrDefault(f => string.Equals(f.ProviderType, provider, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(
                   $"Account '{accountSection.Key}' uses unknown provider '{provider}'. Supported: {Supported(factoryList)}.");
    }

    private string Names() => _accounts.Count == 0 ? "(none)" : string.Join(", ", _accounts.Keys.Order());

    private static string Supported(IEnumerable<IAccountProviderFactory> factories) =>
        string.Join(", ", factories.Select(f => f.ProviderType));
}
