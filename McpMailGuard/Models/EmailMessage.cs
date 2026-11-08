// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

namespace McpMailGuard.Models;

/// <summary>
/// An email message as returned to agents. Properties a provider does not expose are null.
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Provider-specific id; pass it to get_email.
    /// </summary>
    public string? Id { get; set; }

    public string? Subject { get; set; }

    /// <summary>
    /// Sender, as "Name &lt;address&gt;" when a display name is known.
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// Comma-separated To recipients.
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    /// Comma-separated Cc recipients.
    /// </summary>
    public string? Cc { get; set; }

    /// <summary>
    /// When the message was received (UTC).
    /// </summary>
    public DateTimeOffset? ReceivedTime { get; set; }

    /// <summary>
    /// When the message was sent (UTC).
    /// </summary>
    public DateTimeOffset? SentTime { get; set; }

    /// <summary>
    /// A short plain-text excerpt of the body. Set in list and search results.
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// The plain-text body. Set only when a single message is retrieved.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// True when <see cref="Body"/> was cut to the server's maximum length.
    /// </summary>
    public bool? BodyTruncated { get; set; }

    /// <summary>
    /// Importance flag (Low, Normal, High).
    /// </summary>
    public string? Importance { get; set; }

    public bool? IsRead { get; set; }

    public bool? HasAttachments { get; set; }
}
