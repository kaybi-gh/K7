using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Services;
using MediatR;

namespace K7.Server.Application.UnitTests.Common.Behaviours;

[TestFixture]
public class MediaAccessBehaviourTests
{
    private IMediaAccessGuard _accessGuard = null!;
    private bool _nextCalled;

    [SetUp]
    public void SetUp()
    {
        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _nextCalled = false;
    }

    [Test]
    public async Task Handle_ShouldSkipAccessCheck_WhenRequestIsNotMediaScoped()
    {
        var behaviour = new MediaAccessBehaviour<PlainRequest, Unit>(_accessGuard);

        await behaviour.Handle(new PlainRequest(), Next, CancellationToken.None);

        await _accessGuard.DidNotReceiveWithAnyArgs().EnsureAccessAsync(default, default);
        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldEnsureAccess_WhenRequestIsMediaScoped()
    {
        var mediaId = Guid.NewGuid();
        var behaviour = new MediaAccessBehaviour<MediaScopedRequest, Unit>(_accessGuard);

        await behaviour.Handle(new MediaScopedRequest(mediaId), Next, CancellationToken.None);

        await _accessGuard.Received(1).EnsureAccessAsync(mediaId, Arg.Any<CancellationToken>());
        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldNotCallNext_WhenAccessGuardThrows()
    {
        var mediaId = Guid.NewGuid();
        _accessGuard
            .EnsureAccessAsync(mediaId, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new UnauthorizedAccessException());
        var behaviour = new MediaAccessBehaviour<MediaScopedRequest, Unit>(_accessGuard);

        var act = () => behaviour.Handle(new MediaScopedRequest(mediaId), Next, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _nextCalled.Should().BeFalse();
    }

    private Task<Unit> Next()
    {
        _nextCalled = true;
        return Task.FromResult(Unit.Value);
    }

    private sealed class PlainRequest;

    private sealed record MediaScopedRequest(Guid MediaId) : IMediaScopedRequest;
}
