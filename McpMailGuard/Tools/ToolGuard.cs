// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using ModelContextProtocol;

namespace McpMailGuard.Tools;

/// <summary>
/// Shared plumbing for tool methods: argument clamping and turning failures into errors the
/// agent can read (the MCP SDK hides the message of any exception that is not an McpException).
/// </summary>
internal static class ToolGuard
{
    public const int MaxResultsLimit = 50;

    public static int ClampResults(int maxResults) => Math.Clamp(maxResults, 1, MaxResultsLimit);

    public static int ClampDays(int days, int max) => Math.Clamp(days, 0, max);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not McpException)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
