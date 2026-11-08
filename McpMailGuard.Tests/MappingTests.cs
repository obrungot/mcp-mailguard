// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Models;
using McpMailGuard.Providers.Microsoft;
using Microsoft.Graph.Models;

namespace McpMailGuard.Tests;

public class MappingTests
{
    private static Message SampleMessage() => new()
    {
        Id = "m1",
        Subject = "Dinner",
        From = new Recipient { EmailAddress = new EmailAddress { Name = "Kari", Address = "kari@example.com" } },
        ToRecipients =
        [
            new Recipient { EmailAddress = new EmailAddress { Address = "me@hotmail.com", Name = "me@hotmail.com" } },
            new Recipient { EmailAddress = new EmailAddress { Name = "Ola", Address = "ola@example.com" } }
        ],
        BodyPreview = "See you at 7",
        Body = new ItemBody { Content = "See you at 7 at the usual place." },
        IsRead = false,
        HasAttachments = true
    };

    [Fact]
    public void List_mapping_has_preview_but_no_body()
    {
        var mapped = GraphEmailProvider.Map(SampleMessage(), includeBody: false, maxBodyChars: 100);

        Assert.Equal("Kari <kari@example.com>", mapped.From);
        Assert.Equal("me@hotmail.com, Ola <ola@example.com>", mapped.To);
        Assert.Null(mapped.Cc);
        Assert.Equal("See you at 7", mapped.Preview);
        Assert.Null(mapped.Body);
        Assert.Null(mapped.BodyTruncated);
        Assert.True(mapped.HasAttachments);
    }

    [Fact]
    public void Single_message_mapping_includes_truncated_body()
    {
        var mapped = GraphEmailProvider.Map(SampleMessage(), includeBody: true, maxBodyChars: 12);

        Assert.StartsWith("See you at 7", mapped.Body);
        Assert.True(mapped.BodyTruncated);
    }

    private static Event SampleEvent() => new()
    {
        Id = "e1",
        Subject = "Dentist",
        Start = new DateTimeTimeZone { DateTime = "2026-11-10T08:00:00.0000000", TimeZone = "UTC" },
        End = new DateTimeTimeZone { DateTime = "2026-11-10T09:00:00.0000000", TimeZone = "UTC" },
        Location = new Location { DisplayName = "Storgata 1" },
        Organizer = new Recipient { EmailAddress = new EmailAddress { Name = "Clinic", Address = "clinic@example.com" } },
        BodyPreview = "Bring the referral",
        Body = new ItemBody { Content = "Bring the referral letter." },
        IsAllDay = false
    };

    [Fact]
    public void Event_mapping_reads_utc_times_and_people()
    {
        var mapped = GraphCalendarProvider.Map(SampleEvent(), includeBody: false, maxBodyChars: 100);

        Assert.Equal(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero), mapped.Start);
        Assert.Equal("Storgata 1", mapped.Location);
        Assert.Equal("Clinic <clinic@example.com>", mapped.Organizer);
        Assert.Null(mapped.Attendees);
        Assert.Null(mapped.Description);
    }

    [Fact]
    public void Single_event_mapping_includes_description()
    {
        var mapped = GraphCalendarProvider.Map(SampleEvent(), includeBody: true, maxBodyChars: 100);

        Assert.Equal("Bring the referral letter.", mapped.Description);
        Assert.False(mapped.DescriptionTruncated);
    }

    [Theory]
    [InlineData("dentist", true)]
    [InlineData("storgata", true)]
    [InlineData("clinic@example", true)]
    [InlineData("referral", true)]
    [InlineData("football", false)]
    [InlineData(" ", false)]
    public void Event_search_matches_title_location_organizer_and_preview(string query, bool expected)
    {
        var mapped = GraphCalendarProvider.Map(SampleEvent(), includeBody: false, maxBodyChars: 100);

        Assert.Equal(expected, CalendarEventSearch.Matches(mapped, query));
    }
}
