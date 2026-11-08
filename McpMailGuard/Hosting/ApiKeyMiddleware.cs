// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace McpMailGuard.Hosting;

/// <summary>
/// Rejects every request that does not carry <c>Authorization: Bearer &lt;ApiKey&gt;</c>,
/// except the unauthenticated health endpoint.
/// </summary>
public sealed class ApiKeyMiddleware
{
    public const string HealthPath = "/health";

    private readonly RequestDelegate _next;
    private readonly byte[] _expectedHash;

    public ApiKeyMiddleware(RequestDelegate next, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required.", nameof(apiKey));

        _next = next;
        _expectedHash = Hash(apiKey);
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase) || IsAuthorized(context.Request))
            return _next(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private bool IsAuthorized(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var presented = header[prefix.Length..].Trim();
        if (presented.Length == 0)
            return false;

        // Compare fixed-length hashes so neither the content nor the length of the key leaks via timing.
        return CryptographicOperations.FixedTimeEquals(Hash(presented), _expectedHash);
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
