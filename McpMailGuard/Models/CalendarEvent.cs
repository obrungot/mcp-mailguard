// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// Model representing a calendar event from a supported provider.
/// All properties are optional where providers differ in what metadata they expose.
/// </summary>
public class CalendarEvent
{
    /// <summary>
    /// Unique identifier for the event across the provider.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Title / summary of the event.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description / details of the event.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start date/time (UTC).
    /// </summary>
    public DateTimeOffset? Start { get; set; }

    /// <summary>
    /// End date/time (UTC).
    /// </summary>
    public DateTimeOffset? End { get; set; }

    /// <summary>
    /// Calendar ID this event belongs to.
    /// </summary>
    public string? CalendarId { get; set; }

    /// <summary>
    /// Location of the event.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Whether the event is all-day.
    /// </summary>
    public bool? IsAllDay { get; set; }
}