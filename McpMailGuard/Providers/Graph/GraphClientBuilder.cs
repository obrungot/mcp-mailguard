// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using Azure.Core;
using Azure.Identity;
using McpMailGuard.Models;
using Microsoft.Graph;

namespace McpMailGuard.Providers.Graph;

/// <summary>
/// Factory that creates a configured <see cref="GraphServiceClient"/> for the selected
/// <see cref="GraphAuthMode"/> (app-only client credentials or delegated interactive sign-in).
/// </summary>
public static class GraphClientBuilder
{
    /// <summary>
    /// Builds a <see cref="GraphServiceClient"/> for the configured authentication mode.
    /// </summary>
    public static GraphServiceClient Create(GraphAuthConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId))
            throw new ArgumentException("Graph ClientId is not configured.", nameof(config));

        return config.ResolveAuthMode() switch
        {
            GraphAuthMode.DeviceCode => CreateDelegated(config, useBrowser: false),
            GraphAuthMode.InteractiveBrowser => CreateDelegated(config, useBrowser: true),
            _ => CreateAppOnly(config)
        };
    }

    private static GraphServiceClient CreateAppOnly(GraphAuthConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            throw new ArgumentException("Graph ClientSecret is required for app-only (ClientCredentials) mode.", nameof(config));
        if (string.IsNullOrWhiteSpace(config.TenantId))
            throw new ArgumentException("Graph TenantId is required for app-only (ClientCredentials) mode.", nameof(config));

        var credential = new ClientSecretCredential(config.TenantId, config.ClientId, config.ClientSecret);

        // App-only tokens always use the ".default" scope, which resolves to the application
        // permissions consented on the app registration.
        return new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }

    private static GraphServiceClient CreateDelegated(GraphAuthConfig config, bool useBrowser)
    {
        var scopes = config.Scopes is { Length: > 0 }
            ? config.Scopes
            : new[] { "User.Read", "Mail.Read", "Calendars.Read" };

        var candidates = BuildTenantCandidates(config.TenantId)
            .Select(tenant => (
                Tenant: tenant,
                Credential: (TokenCredential)(useBrowser
                    ? CreateBrowserCredential(config, tenant)
                    : CreateDeviceCodeCredential(config, tenant))))
            .ToList();

        TokenCredential credential = candidates.Count == 1
            ? candidates[0].Credential
            : new TenantFallbackCredential(candidates,
                message => Console.Error.WriteLine($"[McpMailGuard] {message}"));

        return new GraphServiceClient(credential, scopes);
    }

    /// <summary>
    /// Ordered list of tenant endpoints to try. An explicitly configured tenant is tried first,
    /// then the well-known aliases. "consumers" leads the defaults so personal-account
    /// registrations (the primary use case) succeed without extra configuration; "common" and
    /// "organizations" cover work/school accounts.
    /// </summary>
    private static IReadOnlyList<string> BuildTenantCandidates(string? configuredTenant)
    {
        var tenants = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredTenant))
            tenants.Add(configuredTenant.Trim());

        foreach (var fallback in new[] { "consumers", "common", "organizations" })
            if (!tenants.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                tenants.Add(fallback);

        return tenants;
    }

    private static DeviceCodeCredential CreateDeviceCodeCredential(GraphAuthConfig config, string tenantId)
    {
        var options = new DeviceCodeCredentialOptions
        {
            ClientId = config.ClientId,
            TenantId = tenantId,
            DeviceCodeCallback = (code, _) =>
            {
                // CRITICAL: write to STDERR. stdout is reserved for the MCP JSON-RPC protocol;
                // writing the device code to stdout would corrupt the transport and hide the prompt.
                Console.Error.WriteLine();
                Console.Error.WriteLine("================= MICROSOFT SIGN-IN REQUIRED =================");
                Console.Error.WriteLine(code.Message);
                Console.Error.WriteLine("==============================================================");
                Console.Error.WriteLine();
                return Task.CompletedTask;
            }
        };

        if (config.EnableTokenCache)
            options.TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "McpMailGuard" };

        return new DeviceCodeCredential(options);
    }

    private static InteractiveBrowserCredential CreateBrowserCredential(GraphAuthConfig config, string tenantId)
    {
        var options = new InteractiveBrowserCredentialOptions
        {
            ClientId = config.ClientId,
            TenantId = tenantId
        };

        if (config.EnableTokenCache)
            options.TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "McpMailGuard" };

        return new InteractiveBrowserCredential(options);
    }
}
