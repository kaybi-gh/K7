using System.Net.Http;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using NSubstitute.ExceptionExtensions;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class SharedProfileServiceTests
{
    private ISharedProfileApi _api = null!;
    private ISharedProfileLocalCache _cache = null!;
    private SharedProfileService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _api = Substitute.For<ISharedProfileApi>();
        _cache = Substitute.For<ISharedProfileLocalCache>();
        _sut = new SharedProfileService(_api, _cache);
    }

    [Test]
    public async Task VerifyGroupPinAsync_ShouldReturnApiResult_WhenOnline()
    {
        var group = CreateGroup();
        _api.VerifySharedProfilePinAsync(group.Id, "4242", Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.VerifyGroupPinAsync(group, "4242");

        result.Should().BeTrue();
    }

    [Test]
    public async Task VerifyGroupPinAsync_ShouldReturnFalse_WhenApiFailsAndNoLocalHash()
    {
        var group = CreateGroup();
        _api.VerifySharedProfilePinAsync(group.Id, "4242", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());

        var result = await _sut.VerifyGroupPinAsync(group, "4242");

        result.Should().BeFalse();
    }

    [Test]
    public async Task VerifyGroupPinAsync_ShouldFallbackToLocalHash_WhenApiFails()
    {
        var group = CreateGroup() with { PinHash = PinVerifier.Hash("4242") };
        _api.VerifySharedProfilePinAsync(group.Id, "4242", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());

        var result = await _sut.VerifyGroupPinAsync(group, "4242");

        result.Should().BeTrue();
    }

    private static SharedProfileDto CreateGroup() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Couple",
        HostUserId = Guid.NewGuid(),
        HasPin = true,
        Members = []
    };
}
