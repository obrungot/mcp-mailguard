# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

McpMailGuard is a .NET 10 MCP server, served over Streamable HTTP. It gives an AI agent (the target is OpenClaw in another container on an Ubuntu host) **read-only** access to mail and calendars. Today it supports Microsoft accounts via Graph (personal Hotmail is the main use). It can serve several accounts at once, and Gmail is meant to be added later as another provider. `README.md` covers setup; `McpMailGuard/README.md` covers the design.

## Commands

```bash
dotnet build McpMailGuard.slnx -c Release
dotnet test McpMailGuard.slnx                                   # xUnit, McpMailGuard.Tests/
dotnet test McpMailGuard.slnx --filter "FullyQualifiedName~GraphQueryTests"   # one class/test
dotnet run --project McpMailGuard -- login hotmail              # interactive device-code sign-in, then exit
dotnet run --project McpMailGuard                               # serve http://localhost:8080/mcp
docker compose build && docker compose run --rm mailguard login hotmail && docker compose up -d
```

For a local run, put `MailGuard:ApiKey` and `Accounts:<name>:{Provider,ClientId}` in `McpMailGuard/appsettings.Local.json` (git-ignored). The server refuses to start without an API key.

## Hard rules

- **Read-only.** Never add tools or Graph calls that send, delete, move or modify anything, and never request write scopes.
- **The server never prompts for sign-in.** Interactive sign-in happens only in `LoginAsync`, run by the `login` command. Serving code only acquires tokens silently and must fail fast with `AccountNotSignedInException`.
- **SPDX header required.** CI fails any tracked `.cs` file without `// SPDX-License-Identifier: MIT` in its first 5 lines. New files start with:
  ```csharp
  // SPDX-License-Identifier: MIT
  // SPDX-FileCopyrightText: 2026 Olaf Brungot
  ```
- **No secrets in images.** `appsettings.Local.json` is copied to the build output only (`CopyToPublishDirectory=Never`), and `.dockerignore` excludes it.
- Tool errors must be thrown as `McpException` (via `ToolGuard.RunAsync`). Otherwise the MCP SDK hides the message from the agent.

## Architecture

- `Program.cs`: the `login [account]` and default `serve` commands. ASP.NET Core with `MapMcp("/mcp")`, session mode `StatefulForInitializeClients`, and `ApiKeyMiddleware` protecting everything except `/health`. Config is read from the executable's folder (`ContentRootPath = AppContext.BaseDirectory`), then `appsettings.Local.json`, env vars, and the command line.
- `Accounts/`
  - `AccountRegistry` builds one `MailAccount` per `Accounts:<name>` section, using the `IAccountProviderFactory` whose `ProviderType` matches `Provider`. Tools call `Resolve(account)`; the name may be omitted when there is only one account.
  - `AccountStore` gives each account a private folder `<DataDirectory>/accounts/<name>/`.
  - To add a backend, implement a factory plus `IEmailProvider`/`ICalendarProvider` and register the factory in `Program.cs`.
- `Providers/Microsoft/`
  - Sign-in uses MSAL directly (`MsalAccountClient`), not Azure.Identity. Azure.Identity's `DeviceCodeCredential` never stored a reusable refresh token for personal accounts (`no_tokens_found`); don't switch back.
  - The token cache is `<account dir>/msal-cache.bin` via `MsalCacheHelper`: DPAPI-encrypted on Windows, an unprotected file elsewhere, since containers have no keyring.
  - `MsalTokenCredential` (used by `GraphServiceClient`) only calls `AcquireTokenSilent`. The cache helper re-reads the file, so a `login` while the server runs takes effect without a restart.
  - `login` verifies `/me` through `MsalTokenCredential`, so a broken silent path fails at login time.
- Graph gotchas:
  - `$search` can't be combined with `$orderby`.
  - Calendar uses `/me/calendarView` with `Prefer: outlook.timezone="UTC"`.
  - Event search is local matching (`CalendarEventSearch`), because Graph event search is unreliable for personal accounts.
  - `GraphErrors.Describe` maps MSAL/OData errors to actionable messages.
- `Tools/`: tool names are set explicitly (`[McpServerTool(Name = "...")]`), list results return previews, and single items return plain-text bodies truncated to `MailGuard:MaxBodyChars`.

## Docker

The image is `runtime-deps` with a self-contained single-file publish. It runs as uid 10001, and `/data` is a volume (mode 700). It sets `Urls=http://+:8080` and blanks `ASPNETCORE_HTTP_PORTS` so it doesn't conflict with the base image. `docker-compose.yml` publishes no ports and joins the external network `${OPENCLAW_NETWORK:-openclaw}`. When running `docker` from Git Bash on Windows, set `MSYS_NO_PATHCONV=1`, or paths like `/data` get rewritten.
