// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Abstractions;
using McpMailGuard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace McpMailGuard.Providers.Graph;

/// <summary>
/// Email provider backed by Microsoft Graph (Outlook / Microsoft 365 / Outlook.com).
/// </summary>
public class GraphEmailProvider : IEmailProvider
{
    private readonly GraphServiceClient _client;
    private readonly GraphUserScope _scope;
    private readonly ILogger<GraphEmailProvider> _logger;

    // Fields requested from Graph to keep payloads small.
    private static readonly string[] MessageSelect =
    {
        "id", "subject", "from", "toRecipients", "receivedDateTime",
        "sentDateTime", "body", "importance", "isRead", "parentFolderId"
    };

    public GraphEmailProvider(GraphServiceClient client, GraphUserScope scope, ILogger<GraphEmailProvider> logger)
    {
        _client = client;
        _scope = scope;
        _logger = logger;
        _scope.EnsureValid();
    }

    public string ProviderName => "Microsoft Graph (Outlook)";

    public async Task<IReadOnlyList<EmailMessage>> ListMessagesAsync(
        int maxResults = 20,
        string? folder = null,
        string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing messages (max={Max}, folder={Folder}, query={Query})",
            maxResults, folder, searchQuery);

        // OData $search cannot be combined with $orderby, so only order when not searching.
        var search = string.IsNullOrWhiteSpace(searchQuery) ? null : $"\"{searchQuery}\"";
        var orderBy = search is null ? new[] { "receivedDateTime desc" } : null;

        try
        {
            var response = await FetchMessagesAsync(maxResults, MessageSelect, orderBy, search, cancellationToken);
            return MapCollection(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list messages from Microsoft Graph");
            throw GraphErrors.Describe(ex);
        }
    }

    public async Task<EmailMessage?> GetMessageAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting message {Id}", id);

        try
        {
            Message? message;
            if (_scope.UseMe)
            {
                message = await _client.Me.Messages[id]
                    .GetAsync(rc => rc.QueryParameters.Select = MessageSelect, cancellationToken);
            }
            else
            {
                message = await _client.Users[_scope.UserId!].Messages[id]
                    .GetAsync(rc => rc.QueryParameters.Select = MessageSelect, cancellationToken);
            }

            return message is null ? null : Map(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get message {Id}", id);
            throw GraphErrors.Describe(ex);
        }
    }

    public async Task<IReadOnlyList<EmailMessage>> SearchAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching messages for '{Query}' (max={Max})", query, maxResults);

        try
        {
            // OData $search cannot be combined with $orderby.
            var response = await FetchMessagesAsync(
                maxResults, MessageSelect, orderBy: null, search: $"\"{query}\"", cancellationToken);
            return MapCollection(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search messages for '{Query}'", query);
            throw GraphErrors.Describe(ex);
        }
    }

    private async Task<MessageCollectionResponse?> FetchMessagesAsync(
        int? top, string[]? select, string[]? orderBy, string? search, CancellationToken cancellationToken)
    {
        if (_scope.UseMe)
        {
            return await _client.Me.Messages.GetAsync(rc =>
            {
                rc.QueryParameters.Top = top;
                rc.QueryParameters.Select = select;
                rc.QueryParameters.Orderby = orderBy;
                rc.QueryParameters.Search = search;
            }, cancellationToken);
        }

        return await _client.Users[_scope.UserId!].Messages.GetAsync(rc =>
        {
            rc.QueryParameters.Top = top;
            rc.QueryParameters.Select = select;
            rc.QueryParameters.Orderby = orderBy;
            rc.QueryParameters.Search = search;
        }, cancellationToken);
    }

    private static List<EmailMessage> MapCollection(MessageCollectionResponse? response)
    {
        if (response?.Value is null)
            return new List<EmailMessage>();

        return response.Value.Select(Map).ToList();
    }

    private static EmailMessage Map(Message m) => new()
    {
        Id = m.Id,
        Subject = m.Subject,
        From = m.From?.EmailAddress?.Address,
        To = m.ToRecipients is { Count: > 0 }
            ? string.Join(", ", m.ToRecipients.Select(r => r.EmailAddress?.Address))
            : null,
        ReceivedTime = m.ReceivedDateTime,
        SentTime = m.SentDateTime,
        Body = m.Body?.Content,
        Importance = m.Importance?.ToString(),
        IsRead = m.IsRead
    };
}
