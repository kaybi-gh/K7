using K7.Shared.Dtos;
using K7.Shared.Security;

namespace K7.Server.Application.UnitTests.Security;

[TestFixture]
public class PasswordPolicyTests
{
    [Test]
    public void IsSatisfiedBy_ShouldAcceptDefaultCompliantPassword()
    {
        PasswordPolicy.IsSatisfiedBy("Password12").Should().BeTrue();
    }

    [Test]
    public void IsSatisfiedBy_ShouldRejectWeakPassword()
    {
        PasswordPolicy.IsSatisfiedBy("12345").Should().BeFalse();
        PasswordPolicy.IsSatisfiedBy("").Should().BeFalse();
        PasswordPolicy.IsSatisfiedBy(null).Should().BeFalse();
    }

    [Test]
    public void Evaluate_ShouldOmitDisabledRules()
    {
        var policy = new PasswordPolicyDto
        {
            RequiredLength = 6,
            RequiredUniqueChars = 1,
            RequireDigit = false,
            RequireLowercase = false,
            RequireUppercase = false,
            RequireNonAlphanumeric = true
        };

        var rules = PasswordPolicy.Evaluate("abcdef!", policy);

        rules.Select(r => r.Rule).Should().Equal(PasswordRule.MinLength, PasswordRule.NonAlphanumeric);
        rules.Should().OnlyContain(r => r.IsMet);
    }

    [Test]
    public void Generate_ShouldSatisfyPolicy()
    {
        var policy = new PasswordPolicyDto
        {
            RequiredLength = 12,
            RequiredUniqueChars = 6,
            RequireDigit = true,
            RequireLowercase = true,
            RequireUppercase = true,
            RequireNonAlphanumeric = true
        };

        var password = PasswordPolicy.Generate(policy);

        PasswordPolicy.IsSatisfiedBy(password, policy).Should().BeTrue();
    }

    [Test]
    public void Describe_ShouldOmitDisabledRules()
    {
        var description = PasswordPolicy.Describe(new PasswordPolicyDto
        {
            RequiredLength = 8,
            RequiredUniqueChars = 1,
            RequireDigit = false,
            RequireLowercase = false,
            RequireUppercase = false,
            RequireNonAlphanumeric = false
        });

        description.Should().Contain("8");
        description.Should().NotContain("digit");
        description.Should().NotContain("special");
        description.Should().NotContain("unique");
    }

    [Test]
    public void Normalize_ShouldClampLengthAndUniqueChars()
    {
        var normalized = PasswordPolicy.Normalize(new PasswordPolicyDto
        {
            RequiredLength = 400,
            RequiredUniqueChars = 200
        });

        normalized.RequiredLength.Should().Be(PasswordPolicyDto.MaxRequiredLength);
        normalized.RequiredUniqueChars.Should().Be(PasswordPolicyDto.MaxRequiredLength);
    }
}
