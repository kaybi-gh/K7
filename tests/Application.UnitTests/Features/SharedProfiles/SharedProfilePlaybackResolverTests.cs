using FluentAssertions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using NUnit.Framework;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles;

public class SharedProfilePlaybackResolverTests
{
    private IApplicationDbContext _context = null!;
    private SharedProfilePlaybackResolver _resolver = null!;
    private List<SharedProfile> _groups = null!;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _groups = [];
        _resolver = new SharedProfilePlaybackResolver(_context);
    }

    private void ConfigureSharedProfiles()
    {
        var dbSet = _groups.BuildMockDbSet();
        _context.SharedProfiles.Returns(dbSet);
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenGroupDoesNotExist()
    {
        ConfigureSharedProfiles();
        var result = await _resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenUserIsNotMemberOrHost()
    {
        var hostId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _groups.Add(new SharedProfile
        {
            Id = groupId,
            Name = "Family",
            HostUserId = hostId,
            CreatedByUserId = hostId,
            Members =
            [
                new SharedProfileMember { SharedProfileId = groupId, UserId = hostId },
                new SharedProfileMember { SharedProfileId = groupId, UserId = Guid.NewGuid() }
            ]
        });

        ConfigureSharedProfiles();
        var result = await _resolver.ResolveAsync(groupId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnCoViewers_WhenHostIsValid()
    {
        var hostId = Guid.NewGuid();
        var coViewerId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _groups.Add(new SharedProfile
        {
            Id = groupId,
            Name = "Kay & Marie",
            HostUserId = hostId,
            CreatedByUserId = hostId,
            Members =
            [
                new SharedProfileMember { SharedProfileId = groupId, UserId = hostId },
                new SharedProfileMember { SharedProfileId = groupId, UserId = coViewerId }
            ]
        });

        ConfigureSharedProfiles();
        var result = await _resolver.ResolveAsync(groupId, hostId);

        result.Should().NotBeNull();
        result!.GroupName.Should().Be("Kay & Marie");
        result.CoViewerUserIds.Should().ContainSingle().Which.Should().Be(coViewerId);
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnContext_WhenMemberIsNotHost()
    {
        var hostId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _groups.Add(new SharedProfile
        {
            Id = groupId,
            Name = "Family Night",
            HostUserId = hostId,
            CreatedByUserId = hostId,
            Members =
            [
                new SharedProfileMember { SharedProfileId = groupId, UserId = hostId },
                new SharedProfileMember { SharedProfileId = groupId, UserId = memberId }
            ]
        });

        ConfigureSharedProfiles();
        var result = await _resolver.ResolveAsync(groupId, memberId);

        result.Should().NotBeNull();
        result!.SharedProfileId.Should().Be(groupId);
        result.CoViewerUserIds.Should().Contain(hostId);
        result.CoViewerUserIds.Should().NotContain(memberId);
    }
}
