using System.Net;
using System.Net.Http.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Settings;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Server.Web.Services;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Shares;
using K7.Shared.Json;
using K7.Tests.Helpers.Smoke;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace K7.Server.Web.SmokeTests;

/// <summary>
/// End to end: what an anonymous holder of a share link can and cannot reach over HTTP.
/// </summary>
[TestFixture]
public class PublicShareEndpointsTests
{
    private const string Token = "smoke-share-link-token";
    private const string ProtectedToken = "smoke-protected-link-token";
    private const string SingleUseToken = "smoke-single-use-link-token";
    private const string Password = "open-sesame";

    private static readonly System.Text.Json.JsonSerializerOptions Json = K7JsonSerializerOptions.CreateDefault();

    private SmokeWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _app = null!;
    private Guid _shareId;
    private Guid _serieId;
    private Guid _seasonId;
    private Guid _episodeId;
    private Guid _episodeFileId;
    private Guid _movieId;
    private Guid _movieFileId;
    private Guid _libraryId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new SmokeWebApplicationFactory();

        // The smoke factory replaces the current user with a fixed one. A visitor is told
        // apart by its principal, so these tests need the real one.
        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUser>();
            services.AddScoped<IUser, CurrentUser>();
        }));
        await SeedAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _app.Dispose();
        _factory.Dispose();
    }

    private HttpClient CreateVisitor() => _app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    private static async Task<OpenShareResultDto> OpenAsync(HttpClient client, string token, string? password = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/public/shares/open",
            new OpenShareRequest { Token = token, Password = password },
            Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<OpenShareResultDto>(Json))!;
    }

    [Test]
    public async Task Open_ShouldReturnNotFound_WhenTokenIsUnknown()
    {
        var client = CreateVisitor();

        var response = await client.PostAsJsonAsync(
            "/api/public/shares/open",
            new OpenShareRequest { Token = "unknown-token" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Visitor_ShouldReadMediaInsideTheShare_WhenLinkIsOpened()
    {
        var client = CreateVisitor();
        var opened = await OpenAsync(client, Token);

        opened.Outcome.Should().Be(OpenShareOutcome.Opened);
        opened.ShareId.Should().Be(_shareId);
        opened.RootMediaId.Should().Be(_seasonId);

        var season = await client.GetAsync($"/api/public/shares/{_shareId}/medias/{_seasonId}");
        var episode = await client.GetAsync($"/api/public/shares/{_shareId}/medias/{_episodeId}");
        var session = await client.GetAsync($"/api/public/shares/{_shareId}/session");

        season.StatusCode.Should().Be(HttpStatusCode.OK);
        episode.StatusCode.Should().Be(HttpStatusCode.OK);
        session.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Visitor_ShouldNotLearnDiskPaths_WhenReadingASharedMedia()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        var body = await client.GetStringAsync($"/api/public/shares/{_shareId}/medias/{_episodeId}");

        body.Should().Contain("episode.mkv");
        body.Should().NotContain("/secret/library");
    }

    [Test]
    public async Task Visitor_ShouldGetNotFound_WhenMediaIsOutsideTheShare()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        var parent = await client.GetAsync($"/api/public/shares/{_shareId}/medias/{_serieId}");
        var unrelated = await client.GetAsync($"/api/public/shares/{_shareId}/medias/{_movieId}");

        parent.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unrelated.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Visitor_ShouldBeRefused_WhenCallingTheRegularApi()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        string[] regularRoutes =
        [
            $"/api/medias/{_episodeId}",
            "/api/libraries",
            "/api/users/me",
            "/api/shares",
            "/api/admin/api-keys"
        ];

        foreach (var route in regularRoutes)
        {
            var response = await client.GetAsync(route);

            response.IsSuccessStatusCode.Should().BeFalse("a share visitor must not reach {0}", route);
        }
    }

    [Test]
    public async Task Visitor_ShouldBeRefused_WhenStreamingAFileOutsideTheShare()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        var response = await client.GetAsync($"/api/indexed-files/{_movieFileId}/direct-stream");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Visitor_ShouldBeRefused_WhenDownloadIsNotAllowed()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        var response = await client.GetAsync($"/api/public/shares/{_shareId}/files/{_episodeFileId}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ShareEndpoints_ShouldRefuse_WhenNoShareWasOpened()
    {
        var client = CreateVisitor();

        var response = await client.GetAsync($"/api/public/shares/{_shareId}/medias/{_seasonId}");

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Test]
    public async Task Open_ShouldRequireThePassword_WhenShareIsProtected()
    {
        var client = CreateVisitor();

        var withoutPassword = await OpenAsync(client, ProtectedToken);
        var wrongPassword = await OpenAsync(client, ProtectedToken, "nope");
        var rightPassword = await OpenAsync(client, ProtectedToken, Password);

        withoutPassword.Outcome.Should().Be(OpenShareOutcome.PasswordRequired);
        wrongPassword.Outcome.Should().Be(OpenShareOutcome.InvalidPassword);
        rightPassword.Outcome.Should().Be(OpenShareOutcome.Opened);
    }

    [Test]
    public async Task Open_ShouldRefuseASecondVisitor_WhenAccessLimitIsOne()
    {
        var first = CreateVisitor();
        var second = CreateVisitor();

        var firstOpen = await OpenAsync(first, SingleUseToken);
        var firstAgain = await OpenAsync(first, SingleUseToken);
        var secondOpen = await OpenAsync(second, SingleUseToken);

        firstOpen.Outcome.Should().Be(OpenShareOutcome.Opened);
        firstAgain.Outcome.Should().Be(OpenShareOutcome.Opened, "a returning visitor keeps its session");
        secondOpen.Outcome.Should().Be(OpenShareOutcome.Unavailable);
        secondOpen.Status.Should().Be(MediaShareStatus.Exhausted);
    }

    [Test]
    public async Task Visitor_ShouldBeCutAtOnce_WhenShareIsRevoked()
    {
        var client = CreateVisitor();
        var revokedToken = "smoke-revoked-link-token";
        var revokedShareId = Guid.NewGuid();
        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var creatorId = await context.MediaShares.Where(s => s.Id == _shareId).Select(s => s.CreatedByUserId).SingleAsync();
            context.MediaShares.Add(CreateShare(revokedShareId, _movieId, creatorId, revokedToken));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await OpenAsync(client, revokedToken);
        var before = await client.GetAsync($"/api/public/shares/{revokedShareId}/medias/{_movieId}");

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var share = await context.MediaShares.SingleAsync(s => s.Id == revokedShareId);
            share.RevokedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
            scope.ServiceProvider.GetRequiredService<IShareAccessInvalidator>().Invalidate();
        }

        var after = await client.GetAsync($"/api/public/shares/{revokedShareId}/medias/{_movieId}");

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        after.IsSuccessStatusCode.Should().BeFalse();
    }

    [Test]
    public async Task Visitor_ShouldNotLearnLibraryIds_WhenReadingASharedMedia()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        var body = await client.GetStringAsync($"/api/public/shares/{_shareId}/medias/{_episodeId}");

        body.Should().NotContain(_libraryId.ToString());
    }

    [Test]
    public void PublicShareRoutes_ShouldRequireTheSharePolicy_ExceptOpening()
    {
        var routes = _app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/public/shares", StringComparison.Ordinal) == true)
            .ToArray();

        routes.Should().NotBeEmpty();

        foreach (var endpoint in routes)
        {
            var template = endpoint.RoutePattern.RawText!;
            if (template == "/api/public/shares/open")
            {
                endpoint.Metadata.GetMetadata<IAllowAnonymous>().Should().NotBeNull();
                continue;
            }

            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Should().Contain(Policies.ShareAccess, "{0} must require {1}", template, Policies.ShareAccess);
        }
    }

    [Test]
    public void StreamRoutes_ShouldCarryTheFileId_UnlessKnownToBeClosedToVisitors()
    {
        // The share scope requirement checks the file id of the route, and refuses a visitor
        // on a stream route that has none. A new stream route without it must be reviewed here.
        string[] closedToVisitors =
        [
            "/api/remote-stream-sessions/{sessionId:guid}/{**path}",
            "/api/stream-sessions/{sessionId:guid}/hls/{*file}"
        ];

        var routesWithoutFileId = _app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(data => data.Policy == Policies.StreamAccess))
            .Where(endpoint => endpoint.RoutePattern.Parameters.All(parameter => parameter.Name != "id"))
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Distinct()
            .ToArray();

        routesWithoutFileId.Should().BeEquivalentTo(closedToVisitors);
    }

    [Test]
    public async Task Visitor_ShouldBeRefused_WhenStreamRouteHasNoFileId()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);

        var response = await client.GetAsync($"/api/remote-stream-sessions/{Guid.NewGuid()}/manifest.m3u8");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Progress_ShouldShowTheVisitorToAdministrators_WhenAVisitorPlays()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);
        var deviceId = await GiveVisitorsADeviceAsync();
        var sessionId = Guid.NewGuid();

        var response = await ReportAsync(client, _episodeId, sessionId, PlaybackState.Playing);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var live = _app.Services.GetRequiredService<IActiveStreamTracker>().GetStreamInfo(sessionId);
        live.Should().NotBeNull();
        live!.MediaId.Should().Be(_episodeId);
        live.DeviceId.Should().Be(deviceId);

        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var recorded = await context.MediaPlaybackSessions.AsNoTracking().SingleAsync(s => s.SessionId == sessionId);
        // Recorded under the technical share visitor account, never under the share owner.
        var visitorUserId = recorded.UserId;
        var ownerId = await context.MediaShares.Where(s => s.Id == _shareId).Select(s => s.CreatedByUserId).SingleAsync();
        visitorUserId.Should().NotBe(ownerId);
        (await context.Users.Where(u => u.Id == visitorUserId).Select(u => u.IsActive).SingleAsync()).Should().BeFalse();
        recorded.MediaId.Should().Be(_episodeId);
        recorded.DeviceId.Should().Be(deviceId);
        // Like a guest: history only, no personal state.
        (await context.UserMediaStates.AnyAsync(s => s.UserId == visitorUserId)).Should().BeFalse();
    }

    [Test]
    public async Task Progress_ShouldRecordNothing_WhenMediaIsOutsideTheShare()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);
        await GiveVisitorsADeviceAsync();
        var sessionId = Guid.NewGuid();

        var response = await ReportAsync(client, _movieId, sessionId, PlaybackState.Playing);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _app.Services.GetRequiredService<IActiveStreamTracker>().GetStreamInfo(sessionId).Should().BeNull();
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        (await context.MediaPlaybackSessions.AnyAsync(s => s.SessionId == sessionId)).Should().BeFalse();
    }

    [Test]
    public async Task Progress_ShouldNotTouchSomeoneElsesPlaybackSession_WhenVisitorSendsItsId()
    {
        var client = CreateVisitor();
        await OpenAsync(client, Token);
        await GiveVisitorsADeviceAsync();
        var sessionId = Guid.NewGuid();

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var ownerId = await context.MediaShares.Where(s => s.Id == _shareId).Select(s => s.CreatedByUserId).SingleAsync();
            context.MediaPlaybackSessions.Add(new MediaPlaybackSession
            {
                UserId = ownerId,
                MediaId = _movieId,
                SessionId = sessionId,
                ReferenceId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                LastUpdateAt = DateTime.UtcNow,
                State = PlaybackState.Playing,
                PositionSeconds = 42
            });
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await ReportAsync(client, _episodeId, sessionId, PlaybackState.Playing);

        using var check = _app.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<IApplicationDbContext>()
            .MediaPlaybackSessions.AsNoTracking().SingleAsync(s => s.SessionId == sessionId);
        stored.MediaId.Should().Be(_movieId);
        stored.PositionSeconds.Should().Be(42);
    }

    private Task<HttpResponseMessage> ReportAsync(HttpClient client, Guid mediaId, Guid sessionId, PlaybackState state) =>
        client.PostAsJsonAsync(
            $"/api/public/shares/{_shareId}/progress",
            new ReportShareProgressRequest
            {
                MediaId = mediaId,
                PositionSeconds = 60,
                DurationSeconds = 1200,
                SessionId = sessionId,
                ReferenceId = Guid.NewGuid(),
                State = (int)state
            },
            Json);

    /// <summary>
    /// A visitor gets its technical device when it creates a stream session, which needs a
    /// real media file. Tests attach one directly.
    /// </summary>
    private async Task<Guid> GiveVisitorsADeviceAsync()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var session = await context.MediaShareSessions
            .Where(s => s.ShareId == _shareId && s.DeviceId == null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstAsync();
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceName = "Share visitor",
            ClientType = ClientType.Web,
            DeviceType = DeviceType.Unknown
        };
        context.Devices.Add(device);
        session.Device = device;
        await context.SaveChangesAsync(CancellationToken.None);
        return device.Id;
    }

    private async Task SeedAsync()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();

        await settings.SetAsync(ServerSettingKeys.SetupCompleted, true);
        await settings.SetAsync(
            ApplicationSettingKeys.FeatureFlags,
            new ServerFeatureFlagsDto { PublicSharesEnabled = true });

        var creatorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var libraryId = _libraryId = Guid.NewGuid();
        _serieId = Guid.NewGuid();
        _seasonId = Guid.NewGuid();
        _episodeId = Guid.NewGuid();
        _episodeFileId = Guid.NewGuid();
        _movieId = Guid.NewGuid();
        _movieFileId = Guid.NewGuid();
        _shareId = Guid.NewGuid();

        context.Users.Add(new User { Id = creatorId, DisplayName = "creator" });
        context.UserCapabilityOverrides.Add(new UserCapabilityOverride
        {
            Id = Guid.NewGuid(),
            UserId = creatorId,
            Capability = Capability.CanShareMedia,
            Enabled = true
        });
        context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Serie,
            Title = "Series",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        context.Medias.AddRange(
            new Serie { Id = _serieId, Title = "Serie" },
            new SerieSeason { Id = _seasonId, SerieId = _serieId, SeasonNumber = 1 },
            new SerieEpisode { Id = _episodeId, SerieId = _serieId, SeasonId = _seasonId, EpisodeNumber = 1, Title = "Pilot" },
            new Movie { Id = _movieId, Title = "Movie" });
        context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _serieId, LibraryId = libraryId },
            new MediaLibraryAvailability { MediaId = _seasonId, LibraryId = libraryId },
            new MediaLibraryAvailability { MediaId = _episodeId, LibraryId = libraryId },
            new MediaLibraryAvailability { MediaId = _movieId, LibraryId = libraryId });
        context.IndexedFiles.AddRange(
            CreateFile(_episodeFileId, libraryId, "episode", _episodeId),
            CreateFile(_movieFileId, libraryId, "movie", _movieId));
        context.MediaShares.AddRange(
            CreateShare(_shareId, _seasonId, creatorId, Token),
            CreateShare(Guid.NewGuid(), _movieId, creatorId, ProtectedToken, PinHashHelper.Hash(Password)),
            CreateShare(Guid.NewGuid(), _movieId, creatorId, SingleUseToken, maxAccessCount: 1));

        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static IndexedFile CreateFile(Guid id, Guid libraryId, string name, Guid mediaId) => new()
    {
        Id = id,
        LibraryId = libraryId,
        Name = name,
        Extension = ".mkv",
        Path = $"/secret/library/{name}.mkv",
        Hash = 1,
        Size = 1024,
        MediaId = mediaId
    };

    private static MediaShare CreateShare(
        Guid id,
        Guid rootMediaId,
        Guid creatorId,
        string token,
        string? passwordHash = null,
        int? maxAccessCount = null) => new()
        {
            Id = id,
            RootMediaId = rootMediaId,
            TokenHash = ShareTokenHelper.Hash(token),
            PasswordHash = passwordHash,
            MaxAccessCount = maxAccessCount,
            AllowPlayback = true,
            AllowDownload = false,
            CreatedByUserId = creatorId
        };
}
