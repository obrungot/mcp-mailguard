// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Accounts;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Client;

namespace McpMailGuard.Providers.Microsoft;

/// <summary>
/// Translates low-level Graph and sign-in failures into clear, actionable messages.
/// </summary>
internal static class GraphErrors
{
    /// <summary>
    /// Returns an exception whose message explains the most likely cause of the failure.
    /// </summary>
    public static Exception Describe(Exception ex)
    {
        var notSignedIn = Find<AccountNotSignedInException>(ex);
        if (notSignedIn is not null)
            return notSignedIn;

        var msal = Find<MsalServiceException>(ex);
        if (msal is not null)
        {
            var hint = msal.Message.Contains("AADSTS9002346", StringComparison.OrdinalIgnoreCase) ||
                       msal.Message.Contains("Microsoft Account users only", StringComparison.OrdinalIgnoreCase)
                ? "This app registration is for personal accounts only; set the account's TenantId to 'consumers'."
                : msal.Message.Contains("AADSTS700016", StringComparison.OrdinalIgnoreCase)
                    ? "The ClientId was not found for this TenantId. Personal accounts use TenantId 'consumers'."
                    : "Check the account's ClientId and TenantId, and that the app registration allows public " +
                      "client flows and has the delegated Mail.Read and Calendars.Read permissions.";

            return new InvalidOperationException(
                $"Microsoft sign-in failed ({msal.ErrorCode}): {msal.Message} {hint}", msal);
        }

        var msalClient = Find<MsalClientException>(ex);
        if (msalClient is not null)
            return new InvalidOperationException(
                $"Microsoft sign-in failed ({msalClient.ErrorCode}): {msalClient.Message}", msalClient);

        var odata = Find<ODataError>(ex);
        if (odata is not null)
        {
            var code = odata.Error?.Code ?? odata.ResponseStatusCode.ToString();
            var message = odata.Error?.Message ?? odata.Message;
            return new InvalidOperationException($"Microsoft Graph returned an error ({code}): {message}", odata);
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
