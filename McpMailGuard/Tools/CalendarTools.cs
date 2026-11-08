// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.ComponentModel;
using McpMailGuard.Abstractions;
using McpMailGuard.Accounts;
using McpMailGuard.Models;
using ModelContextProtocol.Server;

namespace McpMailGuard.Tools;

/// <summary>
/// Read-only calendar tools. All times are UTC.
/// </summary>
[McpServerToolType]
public sealed class CalendarTools
{
    private const string AccountDescription =
        "Account name from list_accounts. May be omitted when only one account is configured.";

    private const int MaxDays = 3660;

    private readonly AccountRegistry _accounts;
    private readonly TimeProvider _time;

    public CalendarTools(AccountRegistry accounts, TimeProvider time)
    {
        _accounts = accounts;
        _time = time;
    }

    [McpServerTool(Name = "get_upcoming_events", ReadOnly = true, OpenWorld = false)]
    [Description("Lists calendar events from now until the given number of days ahead, ordered by start time. Recurring events appear as separate occurrences. Times are UTC.")]
    public Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        [Description("How many days ahead to look.")] int days = 14,
        [Description("Maximum number of events to return (1-50).")] int maxResults = 20,
        [Description(AccountDescription)] string? account = null,
        CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var end = now.AddDays(Math.Max(1, ToolGuard.ClampDays(days, MaxDays)));
        return ToolGuard.RunAsync(() => Calendar(account)
            .ListEventsAsync(now, end, ToolGuard.ClampResults(maxResults), cancellationToken));
    }

    [McpServerTool(Name = "search_events", ReadOnly = true, OpenWorld = false)]
    [Description("Finds calendar events whose title, location, organizer, attendees or description contain the query, within a window around today. Times are UTC.")]
    public Task<IReadOnlyList<CalendarEvent>> SearchEventsAsync(
        [Description("Text to look for.")] string query,
        [Description("How many days back from today to include.")] int daysBack = 90,
        [Description("How many days ahead of today to include.")] int daysAhead = 365,
        [Description("Maximum number of events to return (1-50).")] int maxResults = 20,
        [Description(AccountDescription)] string? account = null,
        CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var start = now.AddDays(-ToolGuard.ClampDays(daysBack, MaxDays));
        var end = now.AddDays(ToolGuard.ClampDays(daysAhead, MaxDays));
        return ToolGuard.RunAsync(() => Calendar(account)
            .SearchAsync(query, start, end, ToolGuard.ClampResults(maxResults), cancellationToken));
    }

    [McpServerTool(Name = "get_event", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one calendar event with its full plain-text description. Times are UTC.")]
    public Task<CalendarEvent?> GetEventAsync(
        [Description("Event id from get_upcoming_events or search_events.")] string id,
        [Description(AccountDescription)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.RunAsync(() => Calendar(account).GetEventAsync(id, cancellationToken));

    private ICalendarProvider Calendar(string? account)
    {
        var resolved = _accounts.Resolve(account);
        return resolved.Calendar
               ?? throw new InvalidOperationException($"Account '{resolved.Name}' does not provide a calendar.");
    }
}
