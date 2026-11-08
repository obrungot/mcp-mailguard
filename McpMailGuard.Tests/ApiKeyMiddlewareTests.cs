// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Hosting;
using Microsoft.AspNetCore.Http;

namespace McpMailGuard.Tests;

public class ApiKeyMiddlewareTests
{
    private const string Key = "s3cret-key";

    private static async Task<(int Status, bool NextCalled)> InvokeAsync(string path, string? authorization)
    {
        var nextCalled = false;
        var middleware = new ApiKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, Key);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (authorization is not null)
            context.Request.Headers.Authorization = authorization;

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, nextCalled);
    }

    [Fact]
    public async Task Missing_header_is_rejected()
    {
        var (status, next) = await InvokeAsync("/mcp", null);
        Assert.Equal(401, status);
        Assert.False(next);
    }

    [Theory]
    [InlineData("Bearer wrong")]
    [InlineData("Bearer ")]
    [InlineData("Basic czNjcmV0LWtleQ==")]
    [InlineData("s3cret-key")]
    [InlineData("Bearer s3cret-key-and-more")]
    public async Task Wrong_credentials_are_rejected(string header)
    {
        var (status, next) = await InvokeAsync("/mcp", header);
        Assert.Equal(401, status);
        Assert.False(next);
    }

    [Theory]
    [InlineData("Bearer s3cret-key")]
    [InlineData("bearer s3cret-key")]
    public async Task Correct_key_is_accepted(string header)
    {
        var (_, next) = await InvokeAsync("/mcp", header);
        Assert.True(next);
    }

    [Fact]
    public async Task Health_endpoint_needs_no_key()
    {
        var (_, next) = await InvokeAsync("/health", null);
        Assert.True(next);
    }

    [Fact]
    public void Empty_key_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ApiKeyMiddleware(_ => Task.CompletedTask, " "));
    }
}
