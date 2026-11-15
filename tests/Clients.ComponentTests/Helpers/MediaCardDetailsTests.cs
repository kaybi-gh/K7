using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Enums;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaCardDetailsTests
{
    private static readonly MediaCardViewModel _movie = new()
    {
        Id = "1",
        Title = "Dune",
        ReleaseYear = 2021,
        RuntimeMinutes = 155,
        Rating = 8.04,
        ContentRating = "PG-13",
        Genres = ["Science Fiction", "Adventure"]
    };

    private static IStringLocalizer Localizer()
    {
        var localizer = Substitute.For<IStringLocalizer>();
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call =>
        {
            var args = call.Arg<object[]>();
            var text = call.Arg<string>() == MediaCardDetails.HoursMinutesKey
                ? $"{args[0]} h {args[1]} min"
                : $"{args[0]} min";
            return new LocalizedString(call.Arg<string>(), text);
        });
        return localizer;
    }

    private static string? Build(MediaCardViewModel model, string? subtitle, params CardDetailField[] fields) =>
        MediaCardDetails.Build(model, fields, subtitle, Localizer());

    [Test]
    public void Build_ShouldReturnNothing_WhenNoFieldIsPicked()
    {
        Build(_movie, null).Should().BeNull();
        MediaCardDetails.Build(_movie, null, null, Localizer()).Should().BeNull();
    }

    [Test]
    public void Build_ShouldUseTheFixedOrder_WhateverTheOrderOfThePicks()
    {
        var line = Build(_movie, null, CardDetailField.Genre, CardDetailField.Rating, CardDetailField.Year, CardDetailField.Runtime);

        line.Should().Be(string.Join(
            MediaCardDetails.Separator,
            "2021",
            "2 h 35 min",
            8.0.ToString("0.#") + "/10",
            "Science Fiction"));
    }

    [Test]
    public void Build_ShouldLeaveOutTheYear_WhenTheSubtitleAlreadyShowsIt()
    {
        Build(_movie, "2021", CardDetailField.Year, CardDetailField.ContentRating).Should().Be("PG-13");
        Build(_movie, "2021", CardDetailField.Year).Should().BeNull();
    }

    [Test]
    public void Build_ShouldKeepTheYear_WhenTheSubtitleShowsSomethingElse()
    {
        Build(_movie, "Denis Villeneuve", CardDetailField.Year).Should().Be("2021");
    }

    [Test]
    public void Build_ShouldOnlyShowWhatTheItemHas()
    {
        var bare = new MediaCardViewModel { Id = "2", Title = "Unknown", RuntimeMinutes = 0, Rating = 0 };

        Build(bare, null, Enum.GetValues<CardDetailField>()).Should().BeNull();
    }

    [Test]
    public void Build_ShouldUseMinutesOnly_WhenRuntimeIsUnderAnHour()
    {
        Build(_movie with { RuntimeMinutes = 42 }, null, CardDetailField.Runtime).Should().Be("42 min");
    }
}
