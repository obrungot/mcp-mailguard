// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Models;
using McpMailGuard.Providers.Microsoft;

namespace McpMailGuard.Tests;

public class GraphQueryTests
{
    [Theory]
    [InlineData("inbox", "inbox")]
    [InlineData("Inbox", "inbox")]
    [InlineData("sent", "sentitems")]
    [InlineData("sentitems", "sentitems")]
    [InlineData("deleted", "deleteditems")]
    [InlineData("junk", "junkemail")]
    [InlineData("drafts", "drafts")]
    [InlineData("archive", "archive")]
    [InlineData(" sent ", "sentitems")]
    public void Folder_aliases_map_to_graph_well_known_names(string alias, string expected)
    {
        Assert.Equal(expected, GraphQuery.ResolveFolder(alias));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("all")]
    [InlineData("ALL")]
    public void All_folders_maps_to_null(string? folder)
    {
        Assert.Null(GraphQuery.ResolveFolder(folder));
    }

    [Fact]
    public void Unknown_folder_is_passed_through_as_an_id()
    {
        Assert.Equal("AAMkAGI2Tg==", GraphQuery.ResolveFolder(" AAMkAGI2Tg== "));
    }

    [Theory]
    [InlineData("invoice", "\"invoice\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData(@"a\b", "\"a\\\\b\"")]
    public void Search_text_is_quoted_and_escaped(string query, string expected)
    {
        Assert.Equal(expected, GraphQuery.QuoteSearch(query));
    }

    [Fact]
    public void Empty_search_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => GraphQuery.QuoteSearch("  "));
    }

    [Fact]
    public void Calendar_window_is_formatted_in_utc()
    {
        var value = new DateTimeOffset(2026, 11, 8, 16, 30, 0, TimeSpan.FromHours(1));
        Assert.Equal("2026-11-08T15:30:00Z", GraphQuery.FormatUtc(value));
    }

    [Fact]
    public void Graph_times_without_offset_are_read_as_utc()
    {
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero),
            GraphQuery.ParseUtc("2026-11-08T10:00:00.0000000"));
        Assert.Null(GraphQuery.ParseUtc(null));
        Assert.Null(GraphQuery.ParseUtc("not a date"));
    }

    [Fact]
    public void Long_bodies_are_truncated_with_a_marker()
    {
        var text = BodyText.Truncate(new string('x', 100), 10, out var truncated);

        Assert.True(truncated);
        Assert.Equal(new string('x', 10) + BodyText.TruncationMarker, text);
    }

    [Fact]
    public void Short_bodies_are_left_alone()
    {
        Assert.Equal("hello", BodyText.Truncate("hello", 10, out var truncated));
        Assert.False(truncated);
        Assert.Null(BodyText.Truncate(null, 10, out _));
    }

    [Fact]
    public void Truncation_does_not_split_a_surrogate_pair()
    {
        var text = BodyText.Truncate("ab😀cd", 3, out _);
        Assert.StartsWith("ab" + BodyText.TruncationMarker[..2], text);
    }
}
