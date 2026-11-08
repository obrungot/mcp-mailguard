// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Models;

namespace McpMailGuard.Abstractions;

/// <summary>
/// Read-only access to one account's calendar (Microsoft Graph, later Google Calendar, ...).
/// </summary>
/// <remarks>
/// Time windows are inclusive of events that overlap them, recurring events are expanded into their
/// individual occurrences, and all times are returned in UTC.
/// </remarks>
public interface ICalendarProvider
{
    /// <summary>
    /// Human-readable provider name used for diagnostics.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns events overlapping the window, ordered by start time.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns events in the window whose title, location, organizer or description contains the query.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> SearchAsync(
        string query,
        DateTimeOffset start,
        DateTimeOffset end,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single event, including its description, or null if it does not exist.
    /// </summary>
    Task<CalendarEvent?> GetEventAsync(
        string id,
        CancellationToken cancellationToken = default);
}
