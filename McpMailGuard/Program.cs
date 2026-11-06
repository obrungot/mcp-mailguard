// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Abstractions;
using McpMailGuard.Models;
using McpMailGuard.Providers.Graph;
using McpMailGuard.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is reserved for the MCP protocol).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Load appsettings.json (optional) and environment variables.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

static GraphAuthConfig ReadGraphConfig(IConfiguration configuration) =>
    configuration.GetSection("Graph").Get<GraphAuthConfig>()
    ?? throw new InvalidOperationException("Missing 'Graph' configuration section.");

// ----- Optional pre-authentication mode -----------------------------------
// Run `dotnet run -- --login` (or `docker run -it ... --login`) to perform the
// interactive sign-in once and populate the token cache, so MCP tool calls never
// have to prompt. Especially useful for DeviceCode/InteractiveBrowser modes.
if (args.Any(a => string.Equals(a, "--login", StringComparison.OrdinalIgnoreCase)))
{
    var config = ReadGraphConfig(builder.Configuration);
    var client = GraphClientBuilder.Create(config);

    Console.Error.WriteLine("Authenticating with Microsoft Graph...");
    if (config.ResolveAuthMode() == GraphAuthMode.DeviceCode)
        Console.Error.WriteLine("Watch for the sign-in code below, then visit https://microsoft.com/devicelogin");

    try
    {
        if (config.ResolveAuthMode() != GraphAuthMode.ClientCredentials)
        {
            await client.Me.GetAsync(); // delegated: acts as the signed-in user
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.UserId))
                throw new InvalidOperationException("App-only mode requires 'Graph:UserId'.");
            await client.Users[config.UserId].GetAsync(); // app-only: address a specific mailbox
        }

        Console.Error.WriteLine("Authentication succeeded. The token has been cached.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Authentication failed: {GraphErrors.Describe(ex).Message}");
        return 1;
    }
}

// ----- Microsoft Graph ----------------------------------------------------
builder.Services.Configure<GraphAuthConfig>(builder.Configuration.GetSection("Graph"));

builder.Services.AddSingleton(sp =>
    GraphClientBuilder.Create(ReadGraphConfig(builder.Configuration)));

builder.Services.AddSingleton(sp =>
{
    var config = ReadGraphConfig(builder.Configuration);
    var isDelegated = config.ResolveAuthMode() != GraphAuthMode.ClientCredentials;
    return isDelegated
        ? new GraphUserScope(useMe: true, userId: null)
        : new GraphUserScope(useMe: false, userId: config.UserId);
});

builder.Services.AddSingleton<IEmailProvider, GraphEmailProvider>();
builder.Services.AddSingleton<ICalendarProvider, GraphCalendarProvider>();

// ----- MCP server ---------------------------------------------------------
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MailTools>()
    .WithTools<CalendarTools>();

await builder.Build().RunAsync();
return 0;
