// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// Provider-neutral text matching for calendar search, for backends without server-side event search.
/// </summary>
public static class CalendarEventSearch
{
    /// <summary>
    /// True when the query occurs (case-insensitively) in the title, location, organizer, attendees
    /// or description preview.
    /// </summary>
    public static bool Matches(CalendarEvent calendarEvent, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var q = query.Trim();
        return Contains(calendarEvent.Title, q)
               || Contains(calendarEvent.Location, q)
               || Contains(calendarEvent.Organizer, q)
               || Contains(calendarEvent.Attendees, q)
               || Contains(calendarEvent.Preview, q)
               || Contains(calendarEvent.Description, q);
    }

    private static bool Contains(string? text, string query) =>
        text is not null && text.Contains(query, StringComparison.OrdinalIgnoreCase);
}
