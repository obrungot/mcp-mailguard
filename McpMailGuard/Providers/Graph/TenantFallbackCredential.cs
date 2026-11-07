// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using Azure.Core;

namespace McpMailGuard.Providers.Graph;

/// <summary>
/// A <see cref="TokenCredential"/> that retries token acquisition against alternative Microsoft
/// Entra tenant endpoints when the first one is incompatible with the app registration.
/// </summary>
/// <remarks>
/// Entra app registrations are scoped to an audience. A "personal Microsoft accounts only" app
/// must use the <c>/consumers</c> endpoint, while a work/school app must use its own tenant or
/// <c>/organizations</c>. Using the wrong endpoint fails with an <c>AADSTS9002346</c>-style error
/// <em>before</em> any user interaction, so it is safe to retry another endpoint.
/// </remarks>
internal sealed class TenantFallbackCredential : TokenCredential
{
    private readonly IReadOnlyList<(string Tenant, TokenCredential Credential)> _candidates;
    private readonly Action<string>? _onFallback;

    public TenantFallbackCredential(
        IReadOnlyList<(string Tenant, TokenCredential Credential)> candidates,
        Action<string>? onFallback = null)
    {
        if (candidates is null || candidates.Count == 0)
            throw new ArgumentException("At least one candidate credential is required.", nameof(candidates));

        _candidates = candidates;
        _onFallback = onFallback;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        for (var i = 0; i < _candidates.Count; i++)
        {
            try
            {
                return await _candidates[i].Credential
                    .GetTokenAsync(requestContext, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (i < _candidates.Count - 1 && IsTenantEndpointMismatch(ex))
            {
                // Wrong endpoint for this app registration - try the next candidate tenant.
                _onFallback?.Invoke(
                    $"Sign-in endpoint '{_candidates[i].Tenant}' rejected this app registration; " +
                    $"retrying with '{_candidates[i + 1].Tenant}'.");
            }
        }

        // Unreachable: the final candidate always returns or throws above.
        throw new InvalidOperationException("Token acquisition failed for all candidate tenants.");
    }

    /// <summary>
    /// Returns true when the error indicates the app registration and the sign-in endpoint
    /// (tenant) are mismatched, in which case another endpoint may succeed.
    /// </summary>
    private static bool IsTenantEndpointMismatch(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    if (IsTenantEndpointMismatch(inner))
                        return true;
            }

            var message = e.Message;
            if (message.Contains("AADSTS9002346", StringComparison.OrdinalIgnoreCase) ||   // MSA-only app, needs /consumers
                message.Contains("AADSTS700016", StringComparison.OrdinalIgnoreCase) ||    // app not found in this directory
                message.Contains("AADSTS50059", StringComparison.OrdinalIgnoreCase) ||     // missing tenant identifier
                message.Contains("Microsoft Account users only", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("use the /consumers endpoint", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("use the /organizations endpoint", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
