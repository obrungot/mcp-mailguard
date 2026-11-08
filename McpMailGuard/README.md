# McpMailGuard: design notes

See the [repository README](../README.md) for setup. This page describes how the code is put together.

## Layout

```
McpMailGuard/
├── Program.cs               # commands (serve / login), DI, HTTP pipeline
├── Hosting/                 # MailGuardOptions, API-key middleware, login command
├── Accounts/                # account registry, per-account storage, provider factory contract
├── Abstractions/            # IEmailProvider, ICalendarProvider, folder aliases
├── Models/                  # DTOs returned to agents + small text helpers
├── Providers/Microsoft/     # Microsoft Graph implementation
└── Tools/                   # MCP tools (account, mail, calendar)
```

## Request flow

1. OpenClaw sends an MCP request to `POST /mcp` with `Authorization: Bearer <key>`.
2. `ApiKeyMiddleware` checks the key (everything except `/health` is protected).
3. A tool in `Tools/` resolves the account through `AccountRegistry.Resolve(account)`.
4. The account's `IEmailProvider` / `ICalendarProvider` calls the backend.
5. `ToolGuard` turns failures into `McpException`s so the agent sees the actual message.

## Accounts

Accounts are configured under `Accounts:<name>`, and `Provider` picks an `IAccountProviderFactory`.
At startup `AccountRegistry` asks each account's factory to `Create` it. Creating an account must not
touch the network or prompt. Each account gets a private folder `<DataDirectory>/accounts/<name>/`
from `AccountStore`.

`IAccountProviderFactory.LoginAsync` is the provider's interactive sign-in, run by
`McpMailGuard login <name>` from a terminal. The server itself never prompts.

## Microsoft provider

- Sign-in uses MSAL directly (`MsalAccountClient`): a public client with the configured
  authority (`consumers` by default) and the delegated device-code flow.
- The token cache is `msal-cache.bin` in the account folder, kept in sync with the file by
  `MsalCacheHelper`. On Windows the file is encrypted with DPAPI. Elsewhere it is a plain file
  (containers have no keyring) inside the owner-only account folder.
- `login` signs in and removes any other cached account. It then calls `/me` through the
  serving credential, so a cache that can't be reused silently fails at login, not later.
- When serving, `MsalTokenCredential` only calls `AcquireTokenSilent`. No cached account, or
  `MsalUiRequiredException`, becomes `AccountNotSignedInException`, which reaches the agent
  immediately. The cache helper re-reads the file, so a new login works without a restart.
- Azure.Identity's `DeviceCodeCredential` was tried first and dropped: with a personal account, it
  never stored a refresh token it could reuse (`no_tokens_found`).
- Calendar calls use `/me/calendarView`, so recurring events are expanded, and send
  `Prefer: outlook.timezone="UTC"`. Graph has no reliable event text search for personal accounts,
  so `search_events` scans the window and matches locally (`CalendarEventSearch`).
- Mail calls read from `/me/mailFolders/{wellKnown}/messages`, or `/me/messages` for all folders.
  `$search` can't be combined with `$orderby`, so search results come in Graph's own order.

## Adding Gmail

1. Add `Providers/Google/` with a `GoogleAccountProviderFactory` (`ProviderType = "Google"`) and
   providers built on the Gmail API and the Google Calendar API.
2. Map the folder aliases to labels: `inbox → INBOX`, `sent → SENT`, `drafts → DRAFT`,
   `deleted → TRASH`, `junk → SPAM`, `archive →` mail without the `INBOX` label.
3. Google's device flow doesn't allow Gmail scopes, so `LoginAsync` should:
   - print an OAuth URL with a loopback redirect (`http://127.0.0.1:<port>`);
   - have the user open it on any machine and paste back the URL the browser was redirected to
     (the page itself won't load);
   - exchange the code and store the refresh token in the account folder.
4. Register the factory in `Program.cs`. Nothing else changes.
