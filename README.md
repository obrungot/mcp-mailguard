<div align="center">

# McpMailGuard

**Read-only email & calendar access for AI agents, over the Model Context Protocol.**

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Streamable%20HTTP-1f6feb.svg)](https://modelcontextprotocol.io/)
[![Docker](https://img.shields.io/badge/docker-ready-2496ED.svg)](Dockerfile)

</div>

---

McpMailGuard is an [MCP](https://modelcontextprotocol.io/) server that lets an AI agent **read** your
mail and calendar, and nothing else. It runs as a small, long-lived Docker service that an agent such
as [OpenClaw](https://docs.openclaw.ai/) reaches over HTTP on a private Docker network.

Supported today: **Microsoft personal accounts (Hotmail / Outlook.com / Live)** and Microsoft 365, via
Microsoft Graph. Accounts are pluggable, so Gmail can be added later next to Hotmail on the same server.

## Features

- **Read-only by design**: no send, delete, move or modify tools, and only read permissions are requested.
- **Several accounts on one server**, each with a name the agent passes as `account`.
- **Sign in once** with a device code from a terminal; the server then works without prompting, across restarts.
- **Fails fast** when an account isn't signed in, instead of hanging in the middle of an agent's request.
- **Agent-friendly output**: list results carry short previews, full messages are plain text with a size cap.
- **Hardened container**: non-root, read-only filesystem, no capabilities, no published ports, API key required.

## Tools

| Tool | Description |
| --- | --- |
| `list_accounts` | Lists the configured accounts. |
| `get_recent_emails` | Latest emails in a folder (`inbox`, `sent`, `drafts`, `archive`, `deleted`, `junk`, `all`), newest first. |
| `search_emails` | Full-text search of emails, optionally within a folder. |
| `get_email` | One email with its full plain-text body. |
| `get_upcoming_events` | Events from now until *n* days ahead; recurring events are expanded. |
| `search_events` | Events matching text within a window around today. |
| `get_event` | One event with its full description. |

Every tool except `list_accounts` takes an optional `account` argument; it can be left out when only
one account is configured. All times are UTC.

## Setup on Ubuntu with OpenClaw

### 1. Register an app in Microsoft Entra

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com/) (or the Azure portal) with any Microsoft account.
2. **App registrations → New registration.**
   - **Supported account types:** *Personal Microsoft accounts only*.
   - No redirect URI is needed.
3. Copy the **Application (client) ID** from the overview.
4. **Authentication → Advanced settings → Allow public client flows → Yes**, then save. Device-code sign-in needs this.
5. **API permissions → Add → Microsoft Graph → Delegated:** `Mail.Read`, `Calendars.Read`, `User.Read`, `offline_access`.

No client secret is needed.

### 2. Configure

```bash
git clone <this repo> mcp-mailguard && cd mcp-mailguard
cp .env.example .env
openssl rand -base64 32        # use the output as MailGuard__ApiKey
nano .env                      # set MailGuard__ApiKey and Accounts__hotmail__ClientId
```

### 3. Share a Docker network with OpenClaw

The server publishes no port on the host. OpenClaw reaches it by container name on a shared network.

```bash
docker network create openclaw
```

Then attach OpenClaw's service to that network in OpenClaw's own `docker-compose.yml`:

```yaml
services:
  openclaw:            # your existing OpenClaw service
    networks: [default, openclaw]
networks:
  openclaw:
    external: true
```

If OpenClaw's compose project already creates a network you'd rather reuse, set `OPENCLAW_NETWORK`
in `.env` to its name instead (see `docker network ls`).

### 4. Build, sign in once, start

```bash
docker compose build
docker compose run --rm mailguard login hotmail
docker compose up -d
```

`login` prints a code. Open <https://microsoft.com/devicelogin> on any device, enter the code and
sign in with your Hotmail account. The sign-in is saved in the `mailguard-data` volume; the server
refreshes it silently from then on. Run `login` again if Microsoft ever revokes it (for example after
a password change); a running server picks up the new sign-in without a restart.

### 5. Connect OpenClaw

Add the server to OpenClaw's config (see [OpenClaw MCP docs](https://docs.openclaw.ai/tools/mcp)):

```json
{
  "mcp": {
    "servers": {
      "mailguard": {
        "url": "http://mailguard:8080/mcp",
        "transport": "streamable-http",
        "headers": { "Authorization": "Bearer <your MailGuard__ApiKey>" }
      }
    }
  }
}
```

Check the connection with `openclaw mcp doctor mailguard --probe`, then ask OpenClaw something like
"what's on my calendar this week?".

## Configuration

Settings come from `appsettings.json` next to the executable, then `appsettings.Local.json`
(git-ignored, local runs only), then environment variables, then command-line options.
Environment variables use `__` between sections.

| Setting | Description |
| --- | --- |
| `MailGuard__ApiKey` | **Required.** Clients must send `Authorization: Bearer <key>`. |
| `MailGuard__DataDirectory` | Where sign-ins are stored. `/data` in the container. |
| `MailGuard__MaxBodyChars` | Max characters of a body/description returned (default 20000). |
| `Accounts__<name>__Provider` | `Microsoft` (Gmail will be `Google`). |
| `Accounts__<name>__ClientId` | Entra application (client) ID. |
| `Accounts__<name>__TenantId` | `consumers` (default, personal accounts), `organizations`, or a tenant id. |
| `Urls` | Listen address. `http://+:8080` in the container, `http://localhost:8080` locally. |

## Security notes

- The API key is the only thing between the network and your mailbox. Keep it long and random, and
  don't publish port 8080 on the host unless you put TLS and the key in front of it.
- The `mailguard-data` volume holds a refresh token that grants read access to your mail and calendar.
  Containers have no OS keyring, so it is stored as a file readable only by the container user
  (uid 10001). Anyone with root on the host can read it. Treat the volume like a password.
- To revoke access, remove the app's consent at <https://account.live.com/consent/Manage> and delete
  the volume (`docker compose down -v`).

## Running locally (development)

```bash
# McpMailGuard/appsettings.Local.json (git-ignored):
# { "MailGuard": { "ApiKey": "dev-key" },
#   "Accounts": { "hotmail": { "Provider": "Microsoft", "ClientId": "<app id>" } } }

dotnet run --project McpMailGuard -- login hotmail
dotnet run --project McpMailGuard              # serves http://localhost:8080/mcp
dotnet test McpMailGuard.slnx
```

Try the tools with the MCP Inspector (`npx @modelcontextprotocol/inspector`), using transport
"Streamable HTTP", URL `http://localhost:8080/mcp` and header `Authorization: Bearer dev-key`.

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| Tool error "Account 'hotmail' is not signed in" | Run `docker compose run --rm mailguard login hotmail`. |
| `AADSTS700016` (application not found) | Wrong ClientId, or TenantId doesn't match the app's account type. Personal-only apps use `consumers`. |
| `AADSTS9002346` | The app is personal-accounts-only; set TenantId to `consumers`. |
| `AADSTS7000218` / public client error | Enable **Allow public client flows** on the app registration. |
| OpenClaw gets 401 | The `Authorization` header doesn't match `MailGuard__ApiKey`. |
| OpenClaw can't resolve `mailguard` | Both containers must be on the same Docker network. |
| Server exits with "ApiKey is not set" | Set `MailGuard__ApiKey` in `.env`. |

## Adding a provider (e.g. Gmail)

Implement `IAccountProviderFactory` (in `McpMailGuard/Accounts/`) plus `IEmailProvider` and
`ICalendarProvider`, and register the factory in `Program.cs`. The tools, account registry, API key
and Docker setup don't change. See [`McpMailGuard/README.md`](McpMailGuard/README.md) for the design.

## License

Released under the [MIT License](LICENSE).
