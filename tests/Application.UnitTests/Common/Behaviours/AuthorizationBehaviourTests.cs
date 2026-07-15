using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using MediatR;

namespace K7.Server.Application.UnitTests.Common.Behaviours;

[TestFixture]
public class AuthorizationBehaviourTests
{
    private IUser _user = null!;
    private IIdentityService _identityService = null!;
    private bool _nextCalled;

    [SetUp]
    public void SetUp()
    {
        _user = Substitute.For<IUser>();
        _identityService = Substitute.For<IIdentityService>();
        _nextCalled = false;
    }

    [Test]
    public async Task Handle_ShouldCallNext_WhenNoAuthorizeAttribute()
    {
        var behaviour = CreateBehaviour<UnauthenticatedRequest>();
        var request = new UnauthenticatedRequest();

        await behaviour.Handle(request, Next, CancellationToken.None);

        _nextCalled.Should().BeTrue();
        await _identityService.DidNotReceiveWithAnyArgs().IsInRoleAsync(default!, default!);
    }

    [Test]
    public async Task Handle_ShouldThrowUnauthorized_WhenAuthorizeRequiredAndIdentityMissing()
    {
        _user.IdentityId.Returns((string?)null);
        var behaviour = CreateBehaviour<AuthorizedRequest>();

        var act = () => behaviour.Handle(new AuthorizedRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _nextCalled.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldCallNext_WhenUserHasMatchingRole()
    {
        _user.IdentityId.Returns("user-1");
        _identityService.IsInRoleAsync("user-1", "Administrator").Returns(true);
        var behaviour = CreateBehaviour<RoleAuthorizedRequest>();

        await behaviour.Handle(new RoleAuthorizedRequest(), Next, CancellationToken.None);

        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldCallNext_WhenUserMatchesSecondTrimmedRole()
    {
        _user.IdentityId.Returns("user-1");
        _identityService.IsInRoleAsync("user-1", "Administrator").Returns(false);
        _identityService.IsInRoleAsync("user-1", "User").Returns(true);
        var behaviour = CreateBehaviour<RoleAuthorizedRequest>();

        await behaviour.Handle(new RoleAuthorizedRequest(), Next, CancellationToken.None);

        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenUserHasNoneOfRoles()
    {
        _user.IdentityId.Returns("user-1");
        _identityService.IsInRoleAsync("user-1", Arg.Any<string>()).Returns(false);
        var behaviour = CreateBehaviour<RoleAuthorizedRequest>();

        var act = () => behaviour.Handle(new RoleAuthorizedRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _nextCalled.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldCallNext_WhenAllPoliciesPass()
    {
        _user.IdentityId.Returns("user-1");
        _identityService.AuthorizeAsync("user-1", "CanPurge").Returns(true);
        _identityService.AuthorizeAsync("user-1", "CanManage").Returns(true);
        var behaviour = CreateBehaviour<MultiPolicyRequest>();

        await behaviour.Handle(new MultiPolicyRequest(), Next, CancellationToken.None);

        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenAnyPolicyFails()
    {
        _user.IdentityId.Returns("user-1");
        _identityService.AuthorizeAsync("user-1", "CanPurge").Returns(true);
        _identityService.AuthorizeAsync("user-1", "CanManage").Returns(false);
        var behaviour = CreateBehaviour<MultiPolicyRequest>();

        var act = () => behaviour.Handle(new MultiPolicyRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _nextCalled.Should().BeFalse();
    }

    private AuthorizationBehaviour<TRequest, Unit> CreateBehaviour<TRequest>()
        where TRequest : notnull =>
        new(_user, _identityService);

    private Task<Unit> Next()
    {
        _nextCalled = true;
        return Task.FromResult(Unit.Value);
    }

    private sealed class UnauthenticatedRequest;

    [Authorize]
    private sealed class AuthorizedRequest;

    [Authorize(Roles = "Administrator, User")]
    private sealed class RoleAuthorizedRequest;

    [Authorize(Policy = "CanPurge")]
    [Authorize(Policy = "CanManage")]
    private sealed class MultiPolicyRequest;
}
