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
git clone https://github.com/obrungot/mcp-mailguard.git && cd mcp-mailguard
cp .env.example .env
openssl rand -base64 32        # use the output as MailGuard__ApiKey
nano .env                      # set MailGuard__ApiKey and Accounts__hotmail__ClientId
```

Write values without quotes and without spaces around `=` (`MailGuard__ApiKey=abc...=`).
Plain `docker run --env-file` keeps quotes as part of the value, and the key then won't match.

### 3. Join OpenClaw's Docker network

The server publishes no port on the host. OpenClaw reaches it as `mailguard` on a Docker network
the two containers share. Docker's DNS resolves the container name.

The easiest way is to join the network OpenClaw's compose project already has. Find its name:

```bash
docker network ls          # e.g. "openclaw_default" for a compose project named "openclaw"
```

and put it in `.env`:

```
OPENCLAW_NETWORK=openclaw_default
```

OpenClaw's own setup doesn't change. If you later run `docker compose down` for OpenClaw, its
network is recreated. In that case run `docker compose up -d --force-recreate` here so mailguard
joins the new one.

Alternatively, run `docker network create openclaw` (the default name) and add that network as
`external: true` to both OpenClaw's service and this one.

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

`docker compose down -v` deletes the volume and with it the sign-in.

### 5. Connect OpenClaw

OpenClaw doesn't find the server on its own. Until it's configured, every request except `/health`
gets `401`. Add it to OpenClaw's config yourself rather than pasting the API key into the bot's
chat, where it would end up in the chat history.

1. Find OpenClaw's config folder on the host (usually `~/.openclaw`):

   ```bash
   docker inspect <openclaw container> --format '{{range .Mounts}}{{.Source}}{{println}}{{end}}'
   ```

2. Add this to `openclaw.json` in that folder (see [OpenClaw MCP docs](https://docs.openclaw.ai/tools/mcp)),
   with the key from `.env`:

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

3. Restart OpenClaw (`docker restart <openclaw container>`).

Then ask OpenClaw something like "what are my three latest emails?". OpenClaw's own MCP commands
(`openclaw mcp ...`, run inside its container) can list the server's tools as well.

### 6. Test the connection (optional)

Run these on the host, in the repo folder. Replace `openclaw_default` with your network name.

```bash
# Reachable on the shared network?
docker run --rm --network openclaw_default curlimages/curl -s http://mailguard:8080/health

# Reachable from inside the OpenClaw container (Node image, so use fetch)?
docker exec <openclaw container> node -e "fetch('http://mailguard:8080/health').then(r=>console.log(r.status))"
```

A real MCP call with the key:

```bash
KEY=$(grep '^MailGuard__ApiKey=' .env | cut -d= -f2-)
mcp() { docker run --rm --network openclaw_default curlimages/curl -s -i http://mailguard:8080/mcp \
  -H "Authorization: Bearer $KEY" -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' "$@"; }

# 1. Start a session and copy the mcp-session-id response header.
mcp -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"1"}}}'
SID=<paste the mcp-session-id>

# 2. Finish the handshake and call a tool.
mcp -H "Mcp-Session-Id: $SID" -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'
mcp -H "Mcp-Session-Id: $SID" -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_recent_emails","arguments":{"maxResults":3}}}'
```

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
| OpenClaw gets 401 | mailguard isn't in OpenClaw's config yet, or the `Authorization` header doesn't match `MailGuard__ApiKey` (check for quotes or spaces in `.env`). |
| OpenClaw can't resolve `mailguard` | Both containers must be on the same Docker network: check `OPENCLAW_NETWORK`, then `docker compose up -d --force-recreate`. |
| Worked before, now unreachable after OpenClaw was restarted with `down` | OpenClaw's network was recreated. Run `docker compose up -d --force-recreate`. |
| Server exits with "ApiKey is not set" | Set `MailGuard__ApiKey` in `.env`. |

## Adding a provider (e.g. Gmail)

Implement `IAccountProviderFactory` (in `McpMailGuard/Accounts/`) plus `IEmailProvider` and
`ICalendarProvider`, and register the factory in `Program.cs`. The tools, account registry, API key
and Docker setup don't change. See [`McpMailGuard/README.md`](McpMailGuard/README.md) for the design.

## License

Released under the [MIT License](LICENSE).
