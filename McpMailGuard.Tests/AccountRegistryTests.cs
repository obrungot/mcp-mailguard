// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Accounts;
using Microsoft.Extensions.Configuration;

namespace McpMailGuard.Tests;

public class AccountRegistryTests
{
    private sealed class FakeFactory : IAccountProviderFactory
    {
        public string ProviderType => "Fake";

        public MailAccount Create(string accountName, IConfigurationSection section, AccountStore store, IServiceProvider services) =>
            new(accountName, ProviderType, Email: null, Calendar: null);

        public Task LoginAsync(string accountName, IConfigurationSection section, AccountStore store, TextWriter output, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static AccountRegistry Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        var store = new AccountStore(Path.Combine(Path.GetTempPath(), "mailguard-tests", Guid.NewGuid().ToString("N")));
        return new AccountRegistry(configuration, [new FakeFactory()], store, new EmptyServiceProvider());
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void Single_account_is_used_when_name_is_omitted()
    {
        var registry = Build(("Accounts:hotmail:Provider", "Fake"));

        Assert.Equal("hotmail", registry.Resolve(null).Name);
        Assert.Equal("hotmail", registry.Resolve("  ").Name);
    }

    [Fact]
    public void Names_are_case_insensitive()
    {
        var registry = Build(("Accounts:hotmail:Provider", "fake"));

        Assert.Equal("hotmail", registry.Resolve("HotMail").Name);
    }

    [Fact]
    public void Several_accounts_require_a_name()
    {
        var registry = Build(("Accounts:hotmail:Provider", "Fake"), ("Accounts:gmail:Provider", "Fake"));

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
        Assert.Contains("gmail, hotmail", ex.Message);
        Assert.Equal("gmail", registry.Resolve("gmail").Name);
    }

    [Fact]
    public void Unknown_account_lists_configured_names()
    {
        var registry = Build(("Accounts:hotmail:Provider", "Fake"));

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Resolve("work"));
        Assert.Contains("hotmail", ex.Message);
    }

    [Fact]
    public void No_accounts_gives_a_clear_error()
    {
        var registry = Build();

        Assert.Empty(registry.Accounts);
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
    }

    [Fact]
    public void Unknown_provider_fails_at_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(("Accounts:hotmail:Provider", "Imap")));
        Assert.Contains("Imap", ex.Message);
        Assert.Contains("Fake", ex.Message);
    }

    [Fact]
    public void Missing_provider_fails_at_startup()
    {
        Assert.Throws<InvalidOperationException>(() => Build(("Accounts:hotmail:ClientId", "x")));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a b")]
    [InlineData("")]
    public void Unsafe_account_names_are_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => AccountStore.ValidateName(name));
    }

    [Fact]
    public void Account_directory_is_created_under_data_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "mailguard-tests", Guid.NewGuid().ToString("N"));
        var store = new AccountStore(root);

        var dir = store.GetAccountDirectory("Hotmail");

        Assert.True(Directory.Exists(dir));
        Assert.Equal(Path.Combine(root, "accounts", "hotmail"), dir);
    }
}
