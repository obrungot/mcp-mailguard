// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// A calendar event (or one occurrence of a recurring event) as returned to agents.
/// Properties a provider does not expose are null.
/// </summary>
public class CalendarEvent
{
    /// <summary>
    /// Provider-specific id; pass it to get_event.
    /// </summary>
    public string? Id { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// Start time (UTC). For all-day events this is midnight UTC of the first day.
    /// </summary>
    public DateTimeOffset? Start { get; set; }

    /// <summary>
    /// End time (UTC). For all-day events this is midnight UTC after the last day.
    /// </summary>
    public DateTimeOffset? End { get; set; }

    public bool? IsAllDay { get; set; }

    public string? Location { get; set; }

    /// <summary>
    /// Organizer, as "Name &lt;address&gt;" when a display name is known.
    /// </summary>
    public string? Organizer { get; set; }

    /// <summary>
    /// Comma-separated attendees.
    /// </summary>
    public string? Attendees { get; set; }

    public bool? IsCancelled { get; set; }

    /// <summary>
    /// A short plain-text excerpt of the description. Set in list and search results.
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// The plain-text description. Set only when a single event is retrieved.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// True when <see cref="Description"/> was cut to the server's maximum length.
    /// </summary>
    public bool? DescriptionTruncated { get; set; }
}
