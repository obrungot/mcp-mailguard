// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// Model representing an email message from a supported provider.
/// All properties are optional where providers differ in what metadata they expose.
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Unique identifier for the message across the provider.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Human-readable subject line.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// The sender's address (RFC 5322 format).
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// The recipient(s) address.
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    /// Date/time when the message was sent/received (UTC).
    /// </summary>
    public DateTimeOffset? ReceivedTime { get; set; }

    /// <summary>
    /// Date/time when the message was sent (UTC).
    /// </summary>
    public DateTimeOffset? SentTime { get; set; }

    /// <summary>
    /// Email body content (plain text, HTML if available).
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Importance flag (Low, Normal, High).
    /// </summary>
    public string? Importance { get; set; }

    /// <summary>
    /// Flag indicating whether the message has been read.
    /// </summary>
    public bool? IsRead { get; set; }
}