// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Abstractions;
using McpMailGuard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// Read-only calendar access for a signed-in Microsoft account, based on <c>/me/calendarView</c>
/// so recurring events are expanded into their occurrences.
/// </summary>
public sealed class GraphCalendarProvider : ICalendarProvider
{
    private const string PreferUtc = "outlook.timezone=\"UTC\"";
    private const string PreferTextBody = "outlook.body-content-type=\"text\"";

    /// <summary>
    /// Page size used when scanning a window for search matches.
    /// </summary>
    private const int SearchPageSize = 100;

    /// <summary>
    /// Upper bound on events scanned per search, to keep a wide window from paging forever.
    /// </summary>
    private const int MaxScannedEvents = 2_000;

    private static readonly string[] ListSelect =
    [
        "id", "subject", "start", "end", "isAllDay", "location", "organizer", "attendees", "isCancelled",
        "bodyPreview"
    ];

    private static readonly string[] FullSelect = [.. ListSelect, "body"];

    private readonly GraphServiceClient _client;
    private readonly int _maxBodyChars;
    private readonly ILogger<GraphCalendarProvider> _logger;

    public GraphCalendarProvider(GraphServiceClient client, int maxBodyChars, ILogger<GraphCalendarProvider> logger)
    {
        _client = client;
        _maxBodyChars = maxBodyChars;
        _logger = logger;
    }

    public string ProviderName => "Microsoft Graph (Outlook)";

    public async Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(
        DateTimeOffset start, DateTimeOffset end, int maxResults, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await GetPageAsync(start, end, maxResults, cancellationToken);
            return (response?.Value ?? [])
                .Take(maxResults)
                .Select(e => Map(e, includeBody: false, _maxBodyChars))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to list calendar events");
            throw GraphErrors.Describe(ex);
        }
    }

    public async Task<IReadOnlyList<CalendarEvent>> SearchAsync(
        string query, DateTimeOffset start, DateTimeOffset end, int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("A search query is required.", nameof(query));

        // Graph has no reliable server-side text search for events on personal accounts, so scan
        // the window page by page and match locally.
        try
        {
            var matches = new List<CalendarEvent>();
            var scanned = 0;
            var page = await GetPageAsync(start, end, SearchPageSize, cancellationToken);

            while (page?.Value is { } events)
            {
                foreach (var graphEvent in events)
                {
                    var mapped = Map(graphEvent, includeBody: false, _maxBodyChars);
                    if (CalendarEventSearch.Matches(mapped, query))
                    {
                        matches.Add(mapped);
                        if (matches.Count >= maxResults)
                            return matches;
                    }
                }

                scanned += events.Count;
                if (scanned >= MaxScannedEvents || string.IsNullOrEmpty(page.OdataNextLink))
                    break;

                page = await _client.Me.CalendarView.WithUrl(page.OdataNextLink)
                    .GetAsync(rc => rc.Headers.Add("Prefer", PreferUtc), cancellationToken);
            }

            return matches;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
        {
            _logger.LogWarning(ex, "Failed to search calendar events");
            throw GraphErrors.Describe(ex);
        }
    }

    public async Task<CalendarEvent?> GetEventAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var graphEvent = await _client.Me.Events[id].GetAsync(rc =>
            {
                rc.QueryParameters.Select = FullSelect;
                rc.Headers.Add("Prefer", PreferUtc);
                rc.Headers.Add("Prefer", PreferTextBody);
            }, cancellationToken);

            return graphEvent is null ? null : Map(graphEvent, includeBody: true, _maxBodyChars);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get event {Id}", id);
            throw GraphErrors.Describe(ex);
        }
    }

    private Task<EventCollectionResponse?> GetPageAsync(
        DateTimeOffset start, DateTimeOffset end, int top, CancellationToken cancellationToken) =>
        _client.Me.CalendarView.GetAsync(rc =>
        {
            rc.QueryParameters.StartDateTime = GraphQuery.FormatUtc(start);
            rc.QueryParameters.EndDateTime = GraphQuery.FormatUtc(end);
            rc.QueryParameters.Top = top;
            rc.QueryParameters.Select = ListSelect;
            rc.QueryParameters.Orderby = ["start/dateTime"];
            rc.Headers.Add("Prefer", PreferUtc);
        }, cancellationToken);

    /// <summary>
    /// Maps a Graph event. List results get the preview only; a single event also gets its
    /// (plain-text, truncated) description.
    /// </summary>
    public static CalendarEvent Map(Event e, bool includeBody, int maxBodyChars)
    {
        var attendees = e.Attendees?
            .Select(a => GraphEmailProvider.FormatAddress(a.EmailAddress))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var result = new CalendarEvent
        {
            Id = e.Id,
            Title = e.Subject,
            Start = GraphQuery.ParseUtc(e.Start?.DateTime),
            End = GraphQuery.ParseUtc(e.End?.DateTime),
            IsAllDay = e.IsAllDay,
            Location = string.IsNullOrWhiteSpace(e.Location?.DisplayName) ? null : e.Location.DisplayName,
            Organizer = GraphEmailProvider.FormatAddress(e.Organizer?.EmailAddress),
            Attendees = attendees is { Count: > 0 } ? string.Join(", ", attendees) : null,
            IsCancelled = e.IsCancelled,
            Preview = string.IsNullOrWhiteSpace(e.BodyPreview) ? null : e.BodyPreview
        };

        if (includeBody)
        {
            result.Description = BodyText.Truncate(e.Body?.Content, maxBodyChars, out var truncated);
            result.DescriptionTruncated = truncated;
        }

        return result;
    }
}
