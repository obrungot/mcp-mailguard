# McpMailGuard

An [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server that gives AI agents
**read-only** access to a personal email and calendar account. The first supported backend is
**Microsoft Outlook / Microsoft 365 / Outlook.com via Microsoft Graph**, but the code is built around
provider abstractions so additional backends (e.g. Gmail, IMAP) can be added without touching the
MCP tools or the hosting setup.

The server speaks the MCP **stdio** transport, making it easy to run locally or deploy as a Docker
image on Ubuntu (or any Linux host).

---

## Features

- **Read-only** access to mail and calendar — no send/delete/modify tools are exposed.
- MCP tools:

  | Tool | Description |
  | --- | --- |
  | `get_recent_emails` | Lists the most recent emails (newest first), optional folder + query. |
  | `search_emails` | Full-text search of emails. |
  | `get_email` | Retrieves the full content of a single email by id. |
  | `get_upcoming_events` | Lists upcoming calendar events ordered by start time. |
  | `search_events` | Full-text search of calendar events. |
  | `get_event` | Retrieves the full details of a single event by id. |

- **Provider abstraction** (`IEmailProvider`, `ICalendarProvider`) — swap or add backends via configuration.
- **Delegated sign-in** (device code / interactive browser) so **personal Hotmail/Outlook.com accounts work**,
  plus app-only client credentials for organizational mailboxes.
- Ships as a **self-contained, single-file** binary (no .NET runtime required on the host).

---

## Architecture

```
McpMailGuard/
├── Abstractions/            # Provider contracts (extension points)
│   ├── IEmailProvider.cs
│   └── ICalendarProvider.cs
├── Models/                  # Provider-neutral DTOs + configuration
│   ├── EmailMessage.cs
│   ├── CalendarEvent.cs
│   └── AuthConfig.cs
├── Providers/
│   └── Graph/               # Microsoft Graph implementation
│       ├── GraphClientBuilder.cs      # app-only + delegated auth
│       ├── GraphUserScope.cs          # /me vs /users/{id}
│       ├── GraphErrors.cs             # friendly auth errors
│       ├── GraphEmailProvider.cs
│       └── GraphCalendarProvider.cs
├── Tools/                   # MCP tools exposed to agents
│   ├── MailTools.cs
│   └── CalendarTools.cs
├── Program.cs               # Host + DI wiring
├── appsettings.json
└── Dockerfile
```

---

## Authentication

Two families of auth are supported:

| `Graph:AuthMode` | Use for | Client secret | `Graph:UserId` | Addresses |
| --- | --- | --- | --- | --- |
| `DeviceCode` *(recommended for personal)* | Hotmail/Outlook.com **and** work/school | No | not needed | `/me` |
| `InteractiveBrowser` | Desktop machines | No | not needed | `/me` |
| `ClientCredentials` | App-only, **organizational mailboxes only** | Yes | **required** | `/users/{id}` |

If `AuthMode` is empty it is inferred: a client secret implies `ClientCredentials`, otherwise `DeviceCode`.

### Delegated flow (personal accounts)

On the first tool call a sign-in code is printed to **stderr**. Open
<https://microsoft.com/devicelogin>, enter the code, and finish signing in. With
`Graph:EnableTokenCache=true` (default), the token is cached so subsequent runs are silent.

Required app-registration settings:

- Supported account types include **personal Microsoft accounts**.
- **Authentication → Allow public client flows → Yes**.
- **Delegated** `Mail.Read` and `Calendars.Read` permissions.

### App-only flow (organizational mailboxes)

- **Application** `Mail.Read` / `Calendars.Read` permissions + **admin consent**.
- Provide `Graph:ClientSecret` and `Graph:UserId` (UPN or user id). App-only tokens have no signed-in
  user, so `/me` is unavailable.

### Configuration

Environment variables (`__` maps to the `Graph:` config section):

| Variable | Description |
| --- | --- |
| `Graph__ClientId` | Entra application (client) ID. |
| `Graph__TenantId` | Tenant GUID, `consumers`, `common`, or `organizations`. Optional in delegated modes (auto-selected if empty). |
| `Graph__ClientSecret` | Client secret (app-only mode only). |
| `Graph__AuthMode` | `DeviceCode` \| `InteractiveBrowser` \| `ClientCredentials` (empty = infer). |
| `Graph__UserId` | Mailbox to read in app-only mode. |
| `Graph__EnableTokenCache` | `true`/`false` — persist the delegated token cache. |

---

## Running locally

```bash
cd McpMailGuard
export Graph__AuthMode="DeviceCode"
export Graph__ClientId="<client-id>"
export Graph__TenantId="consumers"
dotnet run -c Release
```

### Pre-authenticate (recommended for delegated modes)

Sign in once so the token is cached, instead of completing the prompt inside a tool call (which the
MCP client may cancel):

```bash
dotnet run -c Release -- --login
# Docker:
docker run -it --rm --env-file .env mcp-mailguard:latest --login
```

The process communicates over stdio; logs and sign-in prompts are written to stderr so they never
corrupt the protocol stream on stdout.

### Configure your MCP client

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

---

## Deploying with Docker (Ubuntu)

1. Copy the environment template and fill it in:

   ```bash
   cp .env.example .env
   # edit .env and set the Graph credentials for your chosen mode
   ```

2. Build the image:

   ```bash
   docker build -t mcp-mailguard:latest .
   ```

3. Run it (stdio transport — keep stdin open with `-i`):

   ```bash
   docker run -i --rm --env-file .env mcp-mailguard:latest
   ```

   Or with Compose:

   ```bash
   docker compose up --build
   ```

---

## Extending to other providers (e.g. Gmail)

Adding a provider requires **no changes** to the MCP tools:

1. Implement `IEmailProvider` and `ICalendarProvider` under `Providers/<YourProvider>/`.
2. Register the implementation in `Program.cs`.
3. Provide the provider-specific credentials through `appsettings.json` / environment variables.

---

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| `TaskCanceledException` / "A task was canceled" | The device-code sign-in never completed; the code is on **stderr**. Sign in at <https://microsoft.com/devicelogin> and don't let the client cancel the call. |
| `AADSTS700016` — application not found | Wrong `Graph:TenantId` for the account type (`consumers` for personal, tenant GUID for work/school). |
| `AADSTS500011` — public client required | Enable **Allow public client flows → Yes**. |
| App-only fails on `/me` | Set `Graph:UserId` or switch to `DeviceCode`; personal accounts can't use app-only. |

---

## Security notes

- The server is **read-only** by design; no mutating tools are exposed.
- Never commit `.env` or secrets. The provided `.gitignore` excludes them.
- Run the container with a read-only filesystem and no capabilities (see `docker-compose.yml`).

---

## License

[MIT](../LICENSE).

## More information

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
- [Microsoft Graph documentation](https://learn.microsoft.com/graph/)
