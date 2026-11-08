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
/// Read-only mail access for a signed-in Microsoft account (Outlook.com / Hotmail / Microsoft 365).
/// </summary>
public sealed class GraphEmailProvider : IEmailProvider
{
    private const string PreferTextBody = "outlook.body-content-type=\"text\"";

    private static readonly string[] ListSelect =
    [
        "id", "subject", "from", "toRecipients", "ccRecipients", "receivedDateTime", "sentDateTime",
        "bodyPreview", "importance", "isRead", "hasAttachments"
    ];

    private static readonly string[] FullSelect = [.. ListSelect, "body"];

    private readonly GraphServiceClient _client;
    private readonly int _maxBodyChars;
    private readonly ILogger<GraphEmailProvider> _logger;

    public GraphEmailProvider(GraphServiceClient client, int maxBodyChars, ILogger<GraphEmailProvider> logger)
    {
        _client = client;
        _maxBodyChars = maxBodyChars;
        _logger = logger;
    }

    public string ProviderName => "Microsoft Graph (Outlook)";

    public Task<IReadOnlyList<EmailMessage>> ListMessagesAsync(
        string? folder, int maxResults, CancellationToken cancellationToken = default) =>
        RunAsync("list messages", () => FetchAsync(folder, maxResults, search: null, cancellationToken));

    public Task<IReadOnlyList<EmailMessage>> SearchAsync(
        string query, string? folder, int maxResults, CancellationToken cancellationToken = default)
    {
        var search = GraphQuery.QuoteSearch(query);
        return RunAsync("search messages", () => FetchAsync(folder, maxResults, search, cancellationToken));
    }

    public async Task<EmailMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _client.Me.Messages[id].GetAsync(rc =>
            {
                rc.QueryParameters.Select = FullSelect;
                rc.Headers.Add("Prefer", PreferTextBody);
            }, cancellationToken);

            return message is null ? null : Map(message, includeBody: true, _maxBodyChars);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get message {Id}", id);
            throw GraphErrors.Describe(ex);
        }
    }

    private async Task<IReadOnlyList<EmailMessage>> FetchAsync(
        string? folder, int maxResults, string? search, CancellationToken cancellationToken)
    {
        // $search cannot be combined with $orderby; Graph orders search results by date itself.
        var orderBy = search is null ? new[] { "receivedDateTime desc" } : null;
        var graphFolder = GraphQuery.ResolveFolder(folder);

        MessageCollectionResponse? response;
        if (graphFolder is null)
        {
            response = await _client.Me.Messages.GetAsync(rc =>
            {
                rc.QueryParameters.Top = maxResults;
                rc.QueryParameters.Select = ListSelect;
                rc.QueryParameters.Orderby = orderBy;
                rc.QueryParameters.Search = search;
            }, cancellationToken);
        }
        else
        {
            response = await _client.Me.MailFolders[graphFolder].Messages.GetAsync(rc =>
            {
                rc.QueryParameters.Top = maxResults;
                rc.QueryParameters.Select = ListSelect;
                rc.QueryParameters.Orderby = orderBy;
                rc.QueryParameters.Search = search;
            }, cancellationToken);
        }

        return (response?.Value ?? [])
            .Take(maxResults)
            .Select(m => Map(m, includeBody: false, _maxBodyChars))
            .ToList();
    }

    private async Task<IReadOnlyList<EmailMessage>> RunAsync(
        string operation, Func<Task<IReadOnlyList<EmailMessage>>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to {Operation}", operation);
            throw GraphErrors.Describe(ex);
        }
    }

    /// <summary>
    /// Maps a Graph message. List results get the preview only; a single message also gets its
    /// (plain-text, truncated) body.
    /// </summary>
    public static EmailMessage Map(Message m, bool includeBody, int maxBodyChars)
    {
        var result = new EmailMessage
        {
            Id = m.Id,
            Subject = m.Subject,
            From = FormatAddress(m.From?.EmailAddress),
            To = FormatRecipients(m.ToRecipients),
            Cc = FormatRecipients(m.CcRecipients),
            ReceivedTime = m.ReceivedDateTime,
            SentTime = m.SentDateTime,
            Preview = m.BodyPreview,
            Importance = m.Importance?.ToString(),
            IsRead = m.IsRead,
            HasAttachments = m.HasAttachments
        };

        if (includeBody)
        {
            result.Body = BodyText.Truncate(m.Body?.Content, maxBodyChars, out var truncated);
            result.BodyTruncated = truncated;
        }

        return result;
    }

    internal static string? FormatAddress(EmailAddress? address)
    {
        if (address is null)
            return null;

        var name = address.Name?.Trim();
        var email = address.Address?.Trim();
        if (string.IsNullOrEmpty(email))
            return string.IsNullOrEmpty(name) ? null : name;

        return string.IsNullOrEmpty(name) || string.Equals(name, email, StringComparison.OrdinalIgnoreCase)
            ? email
            : $"{name} <{email}>";
    }

    internal static string? FormatRecipients(IEnumerable<Recipient>? recipients)
    {
        var formatted = recipients?
            .Select(r => FormatAddress(r.EmailAddress))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        return formatted is { Count: > 0 } ? string.Join(", ", formatted) : null;
    }
}
