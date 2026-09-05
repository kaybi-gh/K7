using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Settings;
using K7.Server.Application.Features.Federation.Commands.ReceivePeerRequest;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Commands;

[TestFixture]
public class ReceivePeerRequestCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IServerSettingsService _settings = null!;
    private IFederationNotifier _notifier = null!;
    private IPeerUrlGuard _peerUrlGuard = null!;
    private ReceivePeerRequestCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _settings = Substitute.For<IServerSettingsService>();
        _settings.GetAsync(ApplicationSettingKeys.FeatureFlags, Arg.Any<CancellationToken>())
            .Returns(new ServerFeatureFlagsDto { FederationInvitationsEnabled = true });

        _notifier = Substitute.For<IFederationNotifier>();
        _peerUrlGuard = Substitute.For<IPeerUrlGuard>();
        _handler = new ReceivePeerRequestCommandHandler(_context, _settings, _notifier, _peerUrlGuard);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldStorePendingRequest_WhenUnderCap()
    {
        await _handler.Handle(new ReceivePeerRequestCommand
        {
            RequesterUrl = "https://peer.example",
            RequesterName = "Peer",
            Token = "token-1"
        }, CancellationToken.None);

        var stored = await _context.PeerRequests.SingleAsync();
        stored.RequesterUrl.Should().Be("https://peer.example");
        stored.Status.Should().Be(PeerRequestStatus.Pending);

        await _notifier.Received(1).NotifyPeerRequestReceivedAsync(
            Arg.Any<K7.Shared.Dtos.Entities.PeerRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldRefreshToken_WhenSameUrlIsAlreadyPending()
    {
        await SeedPendingAsync("https://peer.example", "old-token");

        await _handler.Handle(new ReceivePeerRequestCommand
        {
            RequesterUrl = "https://peer.example",
            RequesterName = "Peer",
            Token = "new-token"
        }, CancellationToken.None);

        var stored = await _context.PeerRequests.SingleAsync();
        stored.Token.Should().Be("new-token");
        (await _context.PeerRequests.CountAsync()).Should().Be(1);

        await _notifier.DidNotReceive().NotifyPeerRequestReceivedAsync(
            Arg.Any<K7.Shared.Dtos.Entities.PeerRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldRejectNewUrl_WhenPendingCapIsReached()
    {
        for (var i = 0; i < ReceivePeerRequestCommandHandler.MaxPendingRequests; i++)
            await SeedPendingAsync($"https://peer-{i}.example", $"token-{i}");

        var act = () => _handler.Handle(new ReceivePeerRequestCommand
        {
            RequesterUrl = "https://overflow.example",
            RequesterName = "Overflow",
            Token = "token-overflow"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pending peer requests*");
        (await _context.PeerRequests.CountAsync()).Should().Be(ReceivePeerRequestCommandHandler.MaxPendingRequests);
    }

    [Test]
    public async Task Handle_ShouldRefreshExisting_WhenPendingCapIsReached()
    {
        await SeedPendingAsync("https://peer.example", "old-token");
        for (var i = 1; i < ReceivePeerRequestCommandHandler.MaxPendingRequests; i++)
            await SeedPendingAsync($"https://peer-{i}.example", $"token-{i}");

        await _handler.Handle(new ReceivePeerRequestCommand
        {
            RequesterUrl = "https://peer.example",
            RequesterName = "Peer",
            Token = "rotated-token"
        }, CancellationToken.None);

        var stored = await _context.PeerRequests
            .SingleAsync(r => r.RequesterUrl == "https://peer.example");
        stored.Token.Should().Be("rotated-token");
    }

    private async Task SeedPendingAsync(string url, string token)
    {
        _context.PeerRequests.Add(new PeerRequest
        {
            Id = Guid.NewGuid(),
            RequesterUrl = url,
            RequesterName = "Seed",
            Token = token,
            Status = PeerRequestStatus.Pending
        });
        await _context.SaveChangesAsync();
    }
}
