// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Accounts;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// One Microsoft account's MSAL public client, with its token cache stored as a file in the
/// account's folder (<c>msal-cache.bin</c>).
/// </summary>
/// <remarks>
/// On Windows the cache file is encrypted with DPAPI. Elsewhere it is a plain file: containers
/// have no OS keyring, and the account folder is readable only by the server's user.
/// The cache helper re-reads the file on every access, so a <c>login</c> run in another process
/// (e.g. <c>docker compose run</c>) is picked up by a running server.
/// </remarks>
public sealed class MsalAccountClient
{
    public const string CacheFileName = "msal-cache.bin";

    private readonly MicrosoftAccountOptions _options;
    private readonly string _accountDirectory;
    private readonly Lazy<Task<IPublicClientApplication>> _app;

    public MsalAccountClient(string accountName, MicrosoftAccountOptions options, string accountDirectory)
    {
        AccountName = accountName;
        _options = options;
        _accountDirectory = accountDirectory;
        _app = new Lazy<Task<IPublicClientApplication>>(CreateAsync);
    }

    public string AccountName { get; }

    public IReadOnlyList<string> Scopes => _options.Scopes;

    public Task<IPublicClientApplication> GetApplicationAsync() => _app.Value;

    /// <summary>
    /// Interactive device-code sign-in. Writes the device-code instructions to <paramref name="output"/>.
    /// </summary>
    public async Task<AuthenticationResult> SignInWithDeviceCodeAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var app = await GetApplicationAsync();
        return await app.AcquireTokenWithDeviceCode(_options.Scopes, async code =>
            {
                await output.WriteLineAsync();
                await output.WriteLineAsync(code.Message);
                await output.WriteLineAsync();
                await output.FlushAsync(cancellationToken);
            })
            .ExecuteAsync(cancellationToken);
    }

    private async Task<IPublicClientApplication> CreateAsync()
    {
        var tenant = string.IsNullOrWhiteSpace(_options.TenantId) ? "consumers" : _options.TenantId.Trim();
        var app = PublicClientApplicationBuilder.Create(_options.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenant)
            .WithDefaultRedirectUri()
            .Build();

        var storage = new StorageCreationPropertiesBuilder(CacheFileName, _accountDirectory);
        if (!OperatingSystem.IsWindows())
            storage.WithUnprotectedFile();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storage.Build());
        cacheHelper.RegisterCache(app.UserTokenCache);

        var cachePath = Path.Combine(_accountDirectory, CacheFileName);
        if (File.Exists(cachePath))
            AccountStore.RestrictToOwner(cachePath);

        return app;
    }
}
