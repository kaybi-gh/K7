using K7.Server.Application.Features.Federation.Services;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.UnitTests.Features.Federation.Services;

[TestFixture]
public class FederationCallbackProofTests
{
    private const string Secret = "shared-assertion-secret";

    [Test]
    public void CreateProviderRevoke_ShouldVerify_WhenUrlHasTrailingSlash()
    {
        var proof = FederationCallbackProof.CreateProviderRevoke("http://provider:7080/", Secret);

        proof.ProviderUrl.Should().Be("http://provider:7080");
        FederationCallbackProof.VerifyProviderRevoke(
            new ProviderRevokeRequest(proof.ProviderUrl + "/", proof.Expires, proof.Signature),
            Secret).Should().BeTrue();
    }

    [Test]
    public void VerifyProviderRevoke_ShouldReject_WhenSecretOrSignatureMismatch()
    {
        var proof = FederationCallbackProof.CreateProviderRevoke("http://provider:7080", Secret);

        FederationCallbackProof.VerifyProviderRevoke(proof, "other-secret").Should().BeFalse();
        FederationCallbackProof.VerifyProviderRevoke(
            proof with { Signature = proof.Signature + "AA" },
            Secret).Should().BeFalse();
        FederationCallbackProof.VerifyProviderRevoke(proof, null).Should().BeFalse();
    }

    [Test]
    public void VerifyProviderRevoke_ShouldReject_WhenExpiredOrTooFarAhead()
    {
        var now = DateTimeOffset.Parse("2026-11-25T12:00:00Z");
        var proof = FederationCallbackProof.CreateProviderRevoke("http://provider:7080", Secret, now);

        FederationCallbackProof.VerifyProviderRevoke(proof, Secret, now.AddMinutes(6)).Should().BeFalse();
        FederationCallbackProof.VerifyProviderRevoke(
            proof with { Expires = now.AddHours(2).ToUnixTimeSeconds() },
            Secret,
            now).Should().BeFalse();
    }
}
