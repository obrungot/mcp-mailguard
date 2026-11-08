// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Accounts;
using McpMailGuard.Hosting;
using McpMailGuard.Providers.Microsoft;
using McpMailGuard.Tools;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

// Commands:
//   McpMailGuard                 serve MCP over Streamable HTTP at /mcp (default)
//   McpMailGuard login [account] interactive sign-in for an account, then exit
var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "serve";
var commandArgs = command == "serve" ? [] : args.Skip(1).Where(a => !a.StartsWith('-')).ToArray();
var hostArgs = args.Where(a => a.StartsWith('-')).ToArray();

// Read configuration next to the executable, so it works whatever directory the server starts in.
// Precedence (lowest to highest): appsettings.json, appsettings.Local.json, environment, command line.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = hostArgs,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(hostArgs);

builder.Services.Configure<MailGuardOptions>(builder.Configuration.GetSection(MailGuardOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp =>
    new AccountStore(sp.GetRequiredService<IOptions<MailGuardOptions>>().Value.ResolveDataDirectory()));

// Account providers. To add a backend (e.g. Gmail), implement IAccountProviderFactory and register it here.
builder.Services.AddSingleton<IAccountProviderFactory, MicrosoftAccountProviderFactory>();
builder.Services.AddSingleton(sp => new AccountRegistry(
    builder.Configuration,
    sp.GetServices<IAccountProviderFactory>(),
    sp.GetRequiredService<AccountStore>(),
    sp));

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
    .WithTools<AccountTools>()
    .WithTools<MailTools>()
    .WithTools<CalendarTools>();

var app = builder.Build();

switch (command)
{
    case "login":
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        return await LoginCommand.RunAsync(
            app.Services, app.Configuration, commandArgs.FirstOrDefault(), Console.Out, Console.Error, cancellation.Token);
    }

    case "serve":
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpMailGuard");
        var options = app.Services.GetRequiredService<IOptions<MailGuardOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            logger.LogCritical(
                "MailGuard:ApiKey is not set (environment variable MailGuard__ApiKey). " +
                "Refusing to serve mail without client authentication.");
            return 1;
        }

        AccountRegistry registry;
        try
        {
            registry = app.Services.GetRequiredService<AccountRegistry>();
        }
        catch (Exception ex)
        {
            logger.LogCritical("Invalid account configuration: {Message}", ex.Message);
            return 1;
        }

        if (registry.Accounts.Count == 0)
            logger.LogWarning("No accounts are configured; tools will report an error until one is added.");
        else
            logger.LogInformation("Serving {Count} account(s): {Accounts}", registry.Accounts.Count,
                string.Join(", ", registry.Accounts.Select(a => $"{a.Name} ({a.ProviderType})")));

        app.UseMiddleware<ApiKeyMiddleware>(options.ApiKey);
        app.MapGet(ApiKeyMiddleware.HealthPath, () => Results.Ok(new { status = "ok" }));
        app.MapMcp("/mcp");

        await app.RunAsync();
        return 0;
    }

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Usage: McpMailGuard [login [account]]");
        return 2;
}
