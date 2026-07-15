using K7.Server.Application.Features.Federation.Services;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.UnitTests.Features.Federation.Services;

[TestFixture]
public class FederationViewerAssertionServiceTests
{
    private readonly FederationViewerAssertionService _service = new();
    private const string Secret = "test-signing-secret";

    [Test]
    public void CreateAndValidate_ShouldRoundTripViewer()
    {
        var viewer = new FederatedUserRef
        {
            OriginUserId = Guid.NewGuid(),
            OriginPeerServerId = Guid.NewGuid(),
            DisplayName = "Kay"
        };

        var assertion = _service.CreateAssertion(viewer, Secret, TimeSpan.FromMinutes(5));
        var validated = _service.ValidateAssertion(assertion, Secret);

        validated.Should().NotBeNull();
        validated!.OriginUserId.Should().Be(viewer.OriginUserId);
        validated.OriginPeerServerId.Should().Be(viewer.OriginPeerServerId);
        validated.DisplayName.Should().Be("Kay");
    }

    [Test]
    public void ValidateAssertion_ShouldReturnNull_WhenSecretMismatch()
    {
        var viewer = new FederatedUserRef { OriginUserId = Guid.NewGuid(), DisplayName = "Kay" };
        var assertion = _service.CreateAssertion(viewer, Secret);

        _service.ValidateAssertion(assertion, "other-secret").Should().BeNull();
    }

    [Test]
    public void ValidateAssertion_ShouldReturnNull_WhenExpired()
    {
        var viewer = new FederatedUserRef { OriginUserId = Guid.NewGuid(), DisplayName = "Kay" };
        var assertion = _service.CreateAssertion(viewer, Secret, TimeSpan.FromSeconds(-1));

        _service.ValidateAssertion(assertion, Secret).Should().BeNull();
    }

    [Test]
    public void ValidateAssertion_ShouldReturnNull_WhenAssertionEmptyOrGarbage()
    {
        _service.ValidateAssertion(null, Secret).Should().BeNull();
        _service.ValidateAssertion(" ", Secret).Should().BeNull();
        _service.ValidateAssertion("%%%", Secret).Should().BeNull();
    }

    [Test]
    public void ValidateAssertion_ShouldReturnNull_WhenPayloadTampered()
    {
        var viewer = new FederatedUserRef { OriginUserId = Guid.NewGuid(), DisplayName = "Kay" };
        var assertion = _service.CreateAssertion(viewer, Secret);
        var bytes = Convert.FromBase64String(assertion);
        bytes[0] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        _service.ValidateAssertion(tampered, Secret).Should().BeNull();
    }
}
