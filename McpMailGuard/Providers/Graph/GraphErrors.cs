// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using Azure.Identity;
using Microsoft.Identity.Client;

namespace McpMailGuard.Providers.Graph;

/// <summary>
/// Translates low-level authentication failures into clear, actionable messages.
/// </summary>
internal static class GraphErrors
{
    /// <summary>
    /// Returns an exception whose message explains the most likely cause of an auth failure.
    /// </summary>
    public static Exception Describe(Exception ex)
    {
        var msal = Find<MsalServiceException>(ex);
        if (msal is not null)
        {
            return new InvalidOperationException(
                $"Microsoft Graph authentication failed ({msal.ErrorCode}): {msal.Message} " +
                "Check Graph:AuthMode, ClientId, TenantId, ClientSecret, and that the required " +
                "delegated/application permissions are consented on the app registration.", msal);
        }

        var uiRequired = Find<MsalUiRequiredException>(ex);
        if (uiRequired is not null)
        {
            return new InvalidOperationException(
                $"Microsoft Graph requires an interactive sign-in ({uiRequired.ErrorCode}): {uiRequired.Message} " +
                "Complete the device-code sign-in when prompted (the code is printed to stderr).", uiRequired);
        }

        var auth = Find<AuthenticationFailedException>(ex);
        if (auth is not null)
        {
            return new InvalidOperationException(
                $"Microsoft Graph authentication failed: {auth.Message} " +
                "For personal (Hotmail/Outlook.com) accounts use Graph:AuthMode=DeviceCode and " +
                "Graph:TenantId=consumers; ensure the app registration allows public client flows.", auth);
        }

        return ex;
    }

    private static T? Find<T>(Exception? ex) where T : Exception
    {
        while (ex is not null)
        {
            if (ex is T match)
                return match;

            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    var found = Find<T>(inner);
                    if (found is not null)
                        return found;
                }
            }

            ex = ex.InnerException;
        }

        return null;
    }
}
