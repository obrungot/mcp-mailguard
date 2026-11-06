<div align="center">

# McpMailGuard

**Read-only email & calendar access for AI agents, over the Model Context Protocol.**

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-stdio-1f6feb.svg)](https://modelcontextprotocol.io/)
[![Docker](https://img.shields.io/badge/docker-ready-2496ED.svg)](Dockerfile)

</div>

---

McpMailGuard is an [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) **stdio** server
that gives AI agents **read-only** access to a personal email and calendar account. The first
supported backend is **Microsoft Outlook / Microsoft 365 / Outlook.com via Microsoft Graph**, but the
code is built around provider abstractions so additional backends (e.g. Gmail, IMAP) can be added
without touching the MCP tools.

It ships as a **self-contained** binary and a hardened **Docker** image, ready to deploy on Ubuntu.

## ✨ Features

- **Read-only by design** — no send, delete, or modify tools are exposed.
- **Six MCP tools** for reading and searching mail and calendar.
- **Works with personal accounts** (Hotmail / Outlook.com) via delegated device-code sign-in.
- **Provider abstraction** (`IEmailProvider`, `ICalendarProvider`) — swap or add backends via configuration.
- **Self-contained, single-file** binary — no .NET runtime required on the host.
- **Hardened Docker recipe** — minimal runtime-deps base, runs as a non-root user, read-only-filesystem friendly.

## 🧰 Tools exposed to agents

| Tool | Description |
| --- | --- |
| `get_recent_emails` | Lists the most recent emails (newest first); optional folder and query. |
| `search_emails` | Full-text search of emails. |
| `get_email` | Retrieves the full content of a single email by id. |
| `get_upcoming_events` | Lists upcoming calendar events ordered by start time. |
| `search_events` | Full-text search of calendar events. |
| `get_event` | Retrieves the full details of a single event by id. |

## 🚀 Quick start

### 1. Register an app in Microsoft Entra ID

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com/).
2. **App registrations → New registration**.
3. **Supported account types** — pick based on your mailbox:
   - **Personal (Hotmail/Outlook.com):** *Personal Microsoft accounts only* (or *Accounts in any organizational directory and personal Microsoft accounts*).
   - **Work/school:** *Accounts in this organizational directory only*.
4. From the **Overview**, note the **Application (client) ID** and **Directory (tenant) ID**.
5. **Authentication → Allow public client flows → Yes** — required for the `DeviceCode` and `InteractiveBrowser` modes.
6. **API permissions → Microsoft Graph** — add the permissions matching your mode:
   - **Delegated** `Mail.Read` and `Calendars.Read` — for `DeviceCode` / `InteractiveBrowser` (recommended).
   - **Application** `Mail.Read` and `Calendars.Read` — for `ClientCredentials` (work/school only), then **Grant admin consent**.
7. Add a **client secret** only if you use `ClientCredentials`.

### 2. Choose an authentication mode

| `Graph:AuthMode` | Use for | Client secret | `Graph:UserId` | Uses `/me` |
| --- | --- | --- | --- | --- |
| `DeviceCode` *(recommended for personal)* | Hotmail/Outlook.com **and** work/school | No | not needed | Yes |
| `InteractiveBrowser` | Desktop machines | No | not needed | Yes |
| `ClientCredentials` | App-only, **organizational mailboxes only** | Yes | **required** | No |

If `AuthMode` is left empty it is inferred: a client secret implies `ClientCredentials`, otherwise `DeviceCode`.

### 3. Configure credentials

Via environment variables (the `__` maps to the `Graph:` config section):

```bash
# Personal Hotmail/Outlook.com — recommended
export Graph__AuthMode="DeviceCode"
export Graph__ClientId="<client-id>"
export Graph__TenantId="consumers"     # "common" also works for mixed account types
# no client secret needed for DeviceCode

# App-only, work/school mailbox (organizational only)
# export Graph__AuthMode="ClientCredentials"
# export Graph__TenantId="<tenant-id>"
# export Graph__ClientSecret="<secret>"
# export Graph__UserId="user@contoso.com"
```

Or copy `.env.example` to `.env` for Docker usage.

### 4. Run it

```bash
# From source
cd McpMailGuard
dotnet run -c Release

# Or with Docker
docker build -t mcp-mailguard:latest .
docker run -i --rm --env-file .env mcp-mailguard:latest
```

In `DeviceCode` mode, sign in **once** so the token is cached (`Graph:EnableTokenCache`) and later
calls are silent. The easiest way is the built-in pre-authentication command, which prompts
*outside* of any MCP tool call (so the client can't time it out):

```bash
dotnet run -c Release -- --login
# Docker:
docker run -it --rm --env-file .env mcp-mailguard:latest --login
```

A sign-in code is printed to **stderr**; open <https://microsoft.com/devicelogin>, enter the code,
and finish signing in. (You can also just call a tool and complete the prompt then.)

> The process communicates over stdio. Logs and sign-in prompts go to **stderr** so they never
> corrupt the JSON-RPC protocol on **stdout**.

## 🔌 Connect an MCP client

**VS Code** (`.vscode/mcp.json`):

```json
{
  "servers": {
    "McpMailGuard": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "<PATH TO McpMailGuard PROJECT>"]
    }
  }
}
```

**Claude Desktop / other clients:**

```json
{
  "mcpServers": {
    "McpMailGuard": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "--env-file", "/path/to/.env", "mcp-mailguard:latest"]
    }
  }
}
```

## 🏗 Architecture

```
McpMailGuard/
├── Abstractions/          # Provider contracts (extension points)
│   ├── IEmailProvider.cs
│   └── ICalendarProvider.cs
├── Models/                # Provider-neutral DTOs + configuration
├── Providers/
│   └── Graph/             # Microsoft Graph implementation
│       ├── GraphClientBuilder.cs      # app-only + delegated auth
│       ├── GraphUserScope.cs          # /me vs /users/{id}
│       ├── GraphEmailProvider.cs
│       └── GraphCalendarProvider.cs
├── Tools/                 # MCP tools exposed to agents
│   ├── MailTools.cs
│   └── CalendarTools.cs
└── Program.cs             # Host + DI wiring + provider selection
```

The MCP tools depend only on the abstractions, never on Graph directly — adding a provider means
implementing `IEmailProvider` / `ICalendarProvider` and registering it in `Program.cs`.

## 🧩 Extending

### Adding a provider (e.g. Gmail)

1. Implement `IEmailProvider` and `ICalendarProvider` under `Providers/<YourProvider>/`.
2. Register the implementation in `Program.cs`, optionally gated by a `Provider` setting.

### Authentication modes

`GraphClientBuilder` supports app-only (`ClientCredentials`) and delegated
(`DeviceCode`, `InteractiveBrowser`) flows. Delegated flows act as the signed-in user (`/me`);
app-only has no user context and addresses `Graph:UserId` via `/users/{id}`.

## 🩺 Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| `TaskCanceledException` / "A task was canceled" during a tool call | The device-code sign-in never completed. The code is printed to **stderr** — check the server log/output window, then sign in at <https://microsoft.com/devicelogin>. Do not let the MCP client cancel the call before you finish. |
| `AADSTS700016` — application not found in directory | Wrong `Graph:TenantId` for the account type. Use `consumers` for personal accounts, or the tenant GUID for work/school. |
| `AADSTS65001` / consent required | Permissions not consented. Use **Delegated** permissions for `DeviceCode`, or grant admin consent for app-only. |
| `AADSTS500011` / public client required | Enable **Authentication → Allow public client flows → Yes** on the app registration. |
| App-only mode fails on `/me` | App-only tokens have no user. Set `Graph:UserId`, or switch to `DeviceCode`. Personal accounts cannot use app-only at all. |
| Corrupted MCP stream / client disconnects | Something wrote to stdout. All logging and prompts must go to **stderr**. |

## 🔒 Security

- The server is **read-only** — no mutating tools are exposed.
- Never commit `.env` or secrets — the provided `.gitignore` excludes them.
- Run the container with a read-only filesystem and no capabilities (see `docker-compose.yml`).
- On Linux, `Graph:EnableTokenCache=true` requires a secret service; disable it in minimal containers.

## 📄 License

Released under the [MIT License](LICENSE).

## 📚 Documentation

See [`McpMailGuard/README.md`](McpMailGuard/README.md) for package-level details.
