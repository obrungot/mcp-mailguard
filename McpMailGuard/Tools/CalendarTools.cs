// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.ComponentModel;
using McpMailGuard.Abstractions;
using McpMailGuard.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpMailGuard.Tools;

/// <summary>
/// Calendar-related MCP tools for agent interaction.
/// </summary>
public class CalendarTools
{
    private readonly ICalendarProvider _calendarProvider;
    private readonly ILogger<CalendarTools> _logger;

    public CalendarTools(ICalendarProvider calendarProvider, ILogger<CalendarTools> logger)
    {
        _calendarProvider = calendarProvider;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Gets a list of upcoming calendar events, ordered by start time.")]
    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        [Description("Maximum number of events to return")] int maxResults = 20,
        [Description("The calendar to read from; 'calendar' means the default calendar")] string calendarId = "calendar",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching upcoming events (calendar={Calendar}, max={Max})", calendarId, maxResults);
        return await _calendarProvider.ListEventsAsync(calendarId, maxResults, cancellationToken);
    }

    [McpServerTool]
    [Description("Searches calendar events for a text query and returns the matches.")]
    public async Task<IReadOnlyList<CalendarEvent>> SearchEventsAsync(
        [Description("Search query text")] string query,
        [Description("Maximum number of events to return")] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching events for query '{Query}' (max={Max})", query, maxResults);
        return await _calendarProvider.SearchAsync(query, maxResults, cancellationToken);
    }

    [McpServerTool]
    [Description("Gets the full details of a single calendar event by its id.")]
    public async Task<CalendarEvent?> GetEventAsync(
        [Description("The event id returned by GetUpcomingEvents or SearchEvents")] string id,
        [Description("The calendar the event belongs to; 'calendar' means the default calendar")] string calendarId = "calendar",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting event {Id}", id);
        return await _calendarProvider.GetEventAsync(id, calendarId, cancellationToken);
    }
}