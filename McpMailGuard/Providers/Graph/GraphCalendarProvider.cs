// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.Globalization;
using McpMailGuard.Abstractions;
using McpMailGuard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace McpMailGuard.Providers.Graph;

/// <summary>
/// Calendar provider backed by Microsoft Graph (Outlook / Microsoft 365 / Outlook.com).
/// </summary>
public class GraphCalendarProvider : ICalendarProvider
{
    private readonly GraphServiceClient _client;
    private readonly GraphUserScope _scope;
    private readonly ILogger<GraphCalendarProvider> _logger;

    // Fields requested from Graph to keep payloads small.
    private static readonly string[] EventSelect =
    {
        "id", "subject", "body", "start", "end", "location", "isAllDay"
    };

    public GraphCalendarProvider(GraphServiceClient client, GraphUserScope scope, ILogger<GraphCalendarProvider> logger)
    {
        _client = client;
        _scope = scope;
        _logger = logger;
        _scope.EnsureValid();
    }

    public string ProviderName => "Microsoft Graph (Outlook)";

    public async Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(
        string calendarId = "calendar",
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing events (calendar={Calendar}, max={Max})", calendarId, maxResults);

        try
        {
            var response = await FetchEventsAsync(maxResults, EventSelect, new[] { "start/dateTime" }, null, cancellationToken);
            return MapCollection(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list calendar events from Microsoft Graph");
            throw GraphErrors.Describe(ex);
        }
    }

    public async Task<CalendarEvent?> GetEventAsync(
        string id,
        string calendarId = "calendar",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting event {Id}", id);

        try
        {
            Event? evt;
            if (_scope.UseMe)
            {
                evt = await _client.Me.Calendar.Events[id]
                    .GetAsync(rc => rc.QueryParameters.Select = EventSelect, cancellationToken);
            }
            else
            {
                evt = await _client.Users[_scope.UserId!].Calendar.Events[id]
                    .GetAsync(rc => rc.QueryParameters.Select = EventSelect, cancellationToken);
            }

            return evt is null ? null : Map(evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get event {Id}", id);
            throw GraphErrors.Describe(ex);
        }
    }

    public async Task<IReadOnlyList<CalendarEvent>> SearchAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching events for '{Query}' (max={Max})", query, maxResults);

        try
        {
            var response = await FetchEventsAsync(
                maxResults, EventSelect, orderBy: null, search: $"\"{query}\"", cancellationToken);
            return MapCollection(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search events for '{Query}'", query);
            throw GraphErrors.Describe(ex);
        }
    }

    private async Task<EventCollectionResponse?> FetchEventsAsync(
        int? top, string[]? select, string[]? orderBy, string? search, CancellationToken cancellationToken)
    {
        if (_scope.UseMe)
        {
            return await _client.Me.Calendar.Events.GetAsync(rc =>
            {
                rc.QueryParameters.Top = top;
                rc.QueryParameters.Select = select;
                rc.QueryParameters.Orderby = orderBy;
                rc.QueryParameters.Search = search;
            }, cancellationToken);
        }

        return await _client.Users[_scope.UserId!].Calendar.Events.GetAsync(rc =>
        {
            rc.QueryParameters.Top = top;
            rc.QueryParameters.Select = select;
            rc.QueryParameters.Orderby = orderBy;
            rc.QueryParameters.Search = search;
        }, cancellationToken);
    }

    private static List<CalendarEvent> MapCollection(EventCollectionResponse? response)
    {
        if (response?.Value is null)
            return new List<CalendarEvent>();

        return response.Value.Select(Map).ToList();
    }

    private static CalendarEvent Map(Event ev) => new()
    {
        Id = ev.Id,
        Title = ev.Subject,
        Description = ev.Body?.Content,
        Start = ParseGraphDateTime(ev.Start),
        End = ParseGraphDateTime(ev.End),
        Location = ev.Location?.DisplayName,
        IsAllDay = ev.IsAllDay
    };

    /// <summary>
    /// Graph returns date/time values as ISO-8601 strings. We parse and normalize to UTC.
    /// </summary>
    private static DateTimeOffset? ParseGraphDateTime(DateTimeTimeZone? value)
    {
        if (value?.DateTime is null)
            return null;

        return DateTimeOffset.TryParse(
            value.DateTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
