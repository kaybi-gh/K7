using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Security;

public class ApiKeyServiceTests
{
    [Test]
    public void HashKey_ShouldBeStable_ForSameSecretAndKey()
    {
        var service = CreateService("test-hash-secret");
        var hash1 = service.HashKey("k7_abc");
        var hash2 = service.HashKey("k7_abc");
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Test]
    public void HashKey_ShouldDiffer_WhenSecretChanges()
    {
        var a = CreateService("secret-a").HashKey("k7_abc");
        var b = CreateService("secret-b").HashKey("k7_abc");
        a.Should().NotBe(b);
    }

    [Test]
    public void GenerateKey_ShouldReturnPrefixedKeyAndMatchingHash()
    {
        var service = CreateService("test-hash-secret");
        var (fullKey, keyHash, keyPrefix) = service.GenerateKey();
        fullKey.Should().StartWith("k7_");
        keyPrefix.Should().Be(fullKey[..11]);
        keyHash.Should().Be(service.HashKey(fullKey));
    }

    [Test]
    public void Constructor_ShouldThrow_WhenHashSecretMissing()
    {
        var act = () => CreateService("");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ApiKeys:HashSecret*");
    }

    private static ApiKeyService CreateService(string hashSecret)
    {
        var context = Substitute.For<K7.Server.Application.Common.Interfaces.IApplicationDbContext>();
        var options = Options.Create(new SecurityConfiguration
        {
            ApiKeys = new ApiKeysSecurityConfiguration { HashSecret = hashSecret }
        });
        return new ApiKeyService(context, options);
    }
}
