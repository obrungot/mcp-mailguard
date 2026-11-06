// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Models;

namespace McpMailGuard.Abstractions;

/// <summary>
/// Abstraction over a calendar data source (Outlook/Microsoft Graph, Google Calendar, ...).
/// Implementations are interchangeable and can be selected at runtime via configuration.
/// </summary>
public interface ICalendarProvider
{
    /// <summary>
    /// Human-readable provider name used for diagnostics and logging.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns a list of calendar events from a specific calendar, ordered by start time.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(
        string calendarId = "calendar",
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single calendar event by its provider-specific id.
    /// </summary>
    Task<CalendarEvent?> GetEventAsync(
        string id,
        string calendarId = "calendar",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches events across calendars.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> SearchAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}