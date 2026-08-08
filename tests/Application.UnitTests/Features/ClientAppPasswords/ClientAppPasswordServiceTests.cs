using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using K7.Server.Infrastructure.Database.Context.Services;
using Microsoft.AspNetCore.DataProtection;

namespace K7.Server.Application.UnitTests.Features.ClientAppPasswords;

public class ClientAppPasswordServiceTests
{
    private static ClientAppPasswordService CreateService() =>
        new(new EphemeralDataProtectionProvider());

    [Test]
    public void HashAndVerify_ShouldSucceed_ForSamePassword()
    {
        var service = CreateService();
        var hash = service.HashPassword("test-password-123");
        service.VerifyPassword(hash, "test-password-123").Should().BeTrue();
    }

    [Test]
    public void VerifyPassword_ShouldFail_ForWrongPassword()
    {
        var service = CreateService();
        var hash = service.HashPassword("correct-password");
        service.VerifyPassword(hash, "wrong-password").Should().BeFalse();
    }

    [Test]
    public void GeneratePassword_ShouldReturnVerifiableHash()
    {
        var service = CreateService();
        var (password, hash) = service.GeneratePassword();
        password.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("k7dp:");
        service.VerifyPassword(hash, password).Should().BeTrue();
    }

    [Test]
    public void VerifyToken_ShouldSucceed_ForSubsonicToken()
    {
        var service = CreateService();
        const string password = "sesame";
        const string salt = "c19b2d";
        var hash = service.HashPassword(password);

#pragma warning disable CA5351
        var token = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password + salt)))
            .ToLowerInvariant();
#pragma warning restore CA5351

        service.VerifyToken(hash, token, salt).Should().BeTrue();
        service.VerifyToken(hash, "deadbeef", salt).Should().BeFalse();
    }

    [Test]
    public void VerifyToken_ShouldFail_ForLegacyIdentityHash()
    {
        var service = CreateService();
        var legacyHash = new Microsoft.AspNetCore.Identity.PasswordHasher<object>()
            .HashPassword(null!, "sesame");

        service.VerifyToken(legacyHash, "anything", "salt").Should().BeFalse();
        service.VerifyPassword(legacyHash, "sesame").Should().BeTrue();
    }
}
