using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Services;

[TestFixture]
public class ContentVisibilityEvaluatorTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IFederationSocialPolicyService _policyService = null!;
    private ContentVisibilityEvaluator _evaluator = null!;

    private Guid _ownerId;
    private Guid _viewerId;
    private Guid _peerServerId;
    private Guid _federatedViewerId;

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

        _policyService = Substitute.For<IFederationSocialPolicyService>();
        _evaluator = new ContentVisibilityEvaluator(_context, _policyService);

        _ownerId = Guid.NewGuid();
        _viewerId = Guid.NewGuid();
        _peerServerId = Guid.NewGuid();
        _federatedViewerId = Guid.NewGuid();

        _context.PeerServers.Add(new PeerServer
        {
            Id = _peerServerId,
            Name = "Peer",
            BaseUrl = "https://peer.example"
        });

        _context.Users.AddRange(
            new User { Id = _ownerId, DisplayName = "owner" },
            new User { Id = _viewerId, DisplayName = "viewer" },
            new User
            {
                Id = _federatedViewerId,
                DisplayName = "federated",
                PeerServerId = _peerServerId,
                OriginUserId = Guid.NewGuid()
            });
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task CanShareAsync_ShouldReturnFalse_WhenScopeIsNobody()
    {
        var result = await _evaluator.CanShareAsync(
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.Nobody);

        result.Should().BeFalse();
    }

    [Test]
    public async Task CanShareAsync_ShouldReturnTrue_WhenScopeIsLocalServer()
    {
        var result = await _evaluator.CanShareAsync(
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.LocalServer);

        result.Should().BeTrue();
    }

    [Test]
    public async Task CanShareAsync_ShouldReturnFalse_WhenFederationDisabled()
    {
        _policyService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new FederationSocialPolicyDto { Enabled = false });

        var result = await _evaluator.CanShareAsync(
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.Federation);

        result.Should().BeFalse();
    }

    [Test]
    public async Task CanViewAsync_ShouldReturnTrue_ForOwner()
    {
        var result = await _evaluator.CanViewAsync(
            _ownerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.Nobody);

        result.Should().BeTrue();
    }

    [Test]
    public async Task CanViewAsync_ShouldReturnFalse_WhenScopeIsNobody()
    {
        var result = await _evaluator.CanViewAsync(
            _viewerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.Nobody);

        result.Should().BeFalse();
    }

    [Test]
    public async Task CanViewAsync_ShouldReturnTrue_WhenSpecificPeopleGrantMatchesLocalUser()
    {
        ConfigureEnabledPolicy(FederationContentType.Reviews);

        _context.VisibilityGrants.Add(new VisibilityGrant
        {
            OwnerUserId = _ownerId,
            ContentType = FederationContentType.Reviews,
            TargetUserId = _viewerId
        });
        await _context.SaveChangesAsync();

        var result = await _evaluator.CanViewAsync(
            _viewerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.SpecificPeople);

        result.Should().BeTrue();
    }

    [Test]
    public async Task CanViewAsync_ShouldReturnFalse_WhenSpecificPeopleGrantDoesNotMatch()
    {
        ConfigureEnabledPolicy(FederationContentType.Reviews);

        _context.VisibilityGrants.Add(new VisibilityGrant
        {
            OwnerUserId = _ownerId,
            ContentType = FederationContentType.Reviews,
            TargetUserId = Guid.NewGuid()
        });
        await _context.SaveChangesAsync();

        var result = await _evaluator.CanViewAsync(
            _viewerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.SpecificPeople);

        result.Should().BeFalse();
    }

    [Test]
    public async Task CanViewAsync_ShouldReturnTrue_WhenSpecificPeopleGrantMatchesFederatedViewerPeer()
    {
        ConfigureEnabledPolicy(FederationContentType.Reviews);

        var federatedUser = await _context.Users.SingleAsync(u => u.Id == _federatedViewerId);

        _context.VisibilityGrants.Add(new VisibilityGrant
        {
            OwnerUserId = _ownerId,
            ContentType = FederationContentType.Reviews,
            TargetPeerServerId = _peerServerId,
            TargetOriginUserId = federatedUser.OriginUserId
        });
        await _context.SaveChangesAsync();

        var result = await _evaluator.CanViewAsync(
            _federatedViewerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.SpecificPeople);

        result.Should().BeTrue();
    }

    [Test]
    public async Task CanViewFederatedAsync_ShouldReturnFalse_WhenScopeIsLocalServer()
    {
        ConfigureEnabledPolicy(FederationContentType.Reviews);

        var result = await _evaluator.CanViewFederatedAsync(
            Guid.NewGuid(),
            _peerServerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.LocalServer);

        result.Should().BeFalse();
    }

    [Test]
    public async Task CanViewFederatedAsync_ShouldReturnTrue_WhenScopeIsFederationAndPolicyEnabled()
    {
        ConfigureEnabledPolicy(FederationContentType.Reviews);

        var result = await _evaluator.CanViewFederatedAsync(
            Guid.NewGuid(),
            _peerServerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.Federation);

        result.Should().BeTrue();
    }

    [Test]
    public async Task CanViewFederatedAsync_ShouldReturnTrue_WhenSpecificPeopleGrantMatchesPeer()
    {
        ConfigureEnabledPolicy(FederationContentType.Reviews);

        var originUserId = Guid.NewGuid();
        _context.VisibilityGrants.Add(new VisibilityGrant
        {
            OwnerUserId = _ownerId,
            ContentType = FederationContentType.Reviews,
            TargetPeerServerId = _peerServerId,
            TargetOriginUserId = originUserId
        });
        await _context.SaveChangesAsync();

        var result = await _evaluator.CanViewFederatedAsync(
            originUserId,
            _peerServerId,
            _ownerId,
            FederationContentType.Reviews,
            VisibilityScope.SpecificPeople);

        result.Should().BeTrue();
    }

    private void ConfigureEnabledPolicy(FederationContentType contentType)
    {
        _policyService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new FederationSocialPolicyDto
            {
                Enabled = true,
                Policies = new Dictionary<FederationContentType, FederationContentTypePolicyDto>
                {
                    [contentType] = new() { Outbound = true, Inbound = true }
                }
            });
    }
}
