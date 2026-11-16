using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components.Shares;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class ShareMediaViewTests
{
    private static readonly Guid ShareId = Guid.NewGuid();

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();

        var publicShare = Substitute.For<IPublicShareService>();
        publicShare.GetDownloadUrl(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(call => $"/download/{call.ArgAt<Guid>(1)}");
        ctx.Services.AddSingleton(publicShare);
        ctx.Services.AddSingleton(Substitute.For<IK7ServerService>());
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        var localizer = Substitute.For<IStringLocalizer<ShareMediaView>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);

        var sharedLocalizer = Substitute.For<IStringLocalizer<SharedResource>>();
        sharedLocalizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(sharedLocalizer);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        return ctx;
    }

    private static OpenShareResultDto Session(bool allowPlayback, bool allowDownload) => new()
    {
        Outcome = OpenShareOutcome.Opened,
        ShareId = ShareId,
        AllowPlayback = allowPlayback,
        AllowDownload = allowDownload
    };

    private static MovieDto Movie(Guid fileId) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Dune",
        IndexedFiles =
        [
            new IndexedFileDto
            {
                Id = fileId,
                LibraryId = Guid.Empty,
                Name = "Dune",
                Extension = ".mkv",
                Path = "Dune.mkv",
                Hash = 0,
                Size = 2048
            }
        ]
    };

    private static MusicAlbumDto Album(params Guid[] trackIds) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Discovery",
        ArtistName = "Daft Punk",
        Tracks = trackIds
            .Select((id, index) => new LiteMusicTrackDto
            {
                Id = id,
                Title = $"Track {index + 1}",
                TrackNumber = index + 1,
                IndexedFileId = Guid.NewGuid()
            })
            .ToList()
    };

    private static IRenderedComponent<ShareMediaView> Render(
        BunitContext ctx,
        MediaDto media,
        OpenShareResultDto session,
        Action<ShareVideoPlayRequest>? onPlayVideo = null,
        Action<ShareAudioPlayRequest>? onPlayAudio = null) =>
        ctx.Render<ShareMediaView>(parameters => parameters
            .Add(p => p.Media, media)
            .Add(p => p.Session, session)
            .Add(p => p.OnPlayVideo, EventCallback.Factory.Create<ShareVideoPlayRequest>(new object(), request => onPlayVideo?.Invoke(request)))
            .Add(p => p.OnPlayAudio, EventCallback.Factory.Create<ShareAudioPlayRequest>(new object(), request => onPlayAudio?.Invoke(request))));

    [Test]
    public void Render_ShouldOfferPlayAndDownload_WhenShareAllowsBoth()
    {
        using var ctx = CreateContext();
        var fileId = Guid.NewGuid();

        var cut = Render(ctx, Movie(fileId), Session(allowPlayback: true, allowDownload: true));

        cut.FindAll(".share-view__play").Should().ContainSingle();
        cut.Markup.Should().Contain($"/download/{fileId}");
    }

    [Test]
    public void Render_ShouldNotOfferDownload_WhenShareOnlyAllowsPlayback()
    {
        using var ctx = CreateContext();

        var cut = Render(ctx, Movie(Guid.NewGuid()), Session(allowPlayback: true, allowDownload: false));

        cut.FindAll(".share-view__play").Should().ContainSingle();
        cut.Markup.Should().NotContain("/download/");
    }

    [Test]
    public void Render_ShouldNotOfferPlay_WhenShareOnlyAllowsDownload()
    {
        using var ctx = CreateContext();

        var cut = Render(ctx, Movie(Guid.NewGuid()), Session(allowPlayback: false, allowDownload: true));

        cut.FindAll(".share-view__play").Should().BeEmpty();
        cut.Markup.Should().Contain("/download/");
    }

    [Test]
    public void Play_ShouldAskForTheMovie_WhenMainButtonIsClicked()
    {
        using var ctx = CreateContext();
        var movie = Movie(Guid.NewGuid());
        ShareVideoPlayRequest? asked = null;

        var cut = Render(ctx, movie, Session(allowPlayback: true, allowDownload: false), onPlayVideo: request => asked = request);
        cut.Find(".share-view__play").Click();

        asked.Should().NotBeNull();
        asked!.MediaId.Should().Be(movie.Id);
    }

    [Test]
    public void Play_ShouldQueueOnlyTheTracksOfThePage_WhenAlbumIsPlayed()
    {
        using var ctx = CreateContext();
        var trackIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ShareAudioPlayRequest? asked = null;

        var cut = Render(ctx, Album(trackIds), Session(allowPlayback: true, allowDownload: false), onPlayAudio: request => asked = request);
        cut.Find(".share-view__play").Click();

        asked.Should().NotBeNull();
        asked!.StartIndex.Should().Be(0);
        asked.Queue.Select(item => item.MediaId).Should().Equal(trackIds);
    }

    [Test]
    public void Render_ShouldShowTheCastWithoutLinks_WhenMediaHasPersonRoles()
    {
        using var ctx = CreateContext();
        var personId = Guid.NewGuid();
        var movie = Movie(Guid.NewGuid()) with
        {
            PersonRoles =
            [
                new LiteActorDto
                {
                    Id = Guid.NewGuid(),
                    Order = 0,
                    CharacterName = "Paul Atreides",
                    Person = new LitePersonDto { Id = personId, Name = "Timothee Chalamet" }
                }
            ]
        };

        var cut = Render(ctx, movie, Session(allowPlayback: true, allowDownload: false));

        var card = cut.Find(".personrole-item");
        card.TagName.Should().BeEquivalentTo("div");
        card.TextContent.Should().Contain("Timothee Chalamet").And.Contain("Paul Atreides");
        // Person pages are not part of a share: nothing leads to them.
        cut.Markup.Should().NotContain("/persons/");
        cut.Markup.Should().NotContain(personId.ToString());
    }

    [Test]
    public void Render_ShouldLinkEpisodesInsideTheShare_WhenMediaIsASeason()
    {
        using var ctx = CreateContext();
        var episodeId = Guid.NewGuid();
        var serieId = Guid.NewGuid();
        var season = new SerieSeasonDto
        {
            Id = Guid.NewGuid(),
            SerieId = serieId,
            SerieTitle = "Dark",
            SeasonNumber = 1,
            Episodes =
            [
                new LiteSerieEpisodeDto { Id = episodeId, Title = "Secrets", EpisodeNumber = 1, SeasonNumber = 1, SerieId = serieId }
            ]
        };

        var cut = Render(ctx, season, Session(allowPlayback: true, allowDownload: false));

        cut.Markup.Should().Contain(SharePaths.Media(ShareId, episodeId));
        // The series is context only: a share never links up to its parents.
        cut.Markup.Should().Contain("Dark");
        cut.Markup.Should().NotContain(serieId.ToString());
    }
}
