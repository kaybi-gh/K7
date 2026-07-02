using FluentAssertions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using NUnit.Framework;

namespace K7.Server.Application.UnitTests.Features.ViewingGroups;

public class ViewingGroupPlaybackResolverTests
{
    private IApplicationDbContext _context = null!;
    private ViewingGroupPlaybackResolver _resolver = null!;
    private List<ViewingGroup> _groups = null!;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _groups = [];
        _resolver = new ViewingGroupPlaybackResolver(_context);
    }

    private void ConfigureViewingGroups()
    {
        var dbSet = _groups.BuildMockDbSet();
        _context.ViewingGroups.Returns(dbSet);
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenGroupDoesNotExist()
    {
        ConfigureViewingGroups();
        var result = await _resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenUserIsNotHost()
    {
        var hostId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _groups.Add(new ViewingGroup
        {
            Id = groupId,
            Name = "Family",
            HostUserId = hostId,
            CreatedByUserId = hostId,
            Members =
            [
                new ViewingGroupMember { ViewingGroupId = groupId, UserId = hostId },
                new ViewingGroupMember { ViewingGroupId = groupId, UserId = Guid.NewGuid() }
            ]
        });

        ConfigureViewingGroups();
        var result = await _resolver.ResolveAsync(groupId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnCoViewers_WhenHostIsValid()
    {
        var hostId = Guid.NewGuid();
        var coViewerId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _groups.Add(new ViewingGroup
        {
            Id = groupId,
            Name = "Kay & Marie",
            HostUserId = hostId,
            CreatedByUserId = hostId,
            Members =
            [
                new ViewingGroupMember { ViewingGroupId = groupId, UserId = hostId },
                new ViewingGroupMember { ViewingGroupId = groupId, UserId = coViewerId }
            ]
        });

        ConfigureViewingGroups();
        var result = await _resolver.ResolveAsync(groupId, hostId);

        result.Should().NotBeNull();
        result!.GroupName.Should().Be("Kay & Marie");
        result.CoViewerUserIds.Should().ContainSingle().Which.Should().Be(coViewerId);
    }
}
