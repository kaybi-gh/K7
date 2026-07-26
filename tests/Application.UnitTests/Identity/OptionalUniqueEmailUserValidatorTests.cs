using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Identity;

[TestFixture]
public class OptionalUniqueEmailUserValidatorTests
{
    private OptionalUniqueEmailUserValidator _validator = null!;
    private UserManager<ApplicationUser> _userManager = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new OptionalUniqueEmailUserValidator();

        var store = Substitute.For<IUserEmailStore<ApplicationUser>>();
        _userManager = Substitute.ForPartsOf<UserManager<ApplicationUser>>(
            store,
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
    }

    [Test]
    public async Task ValidateAsync_ShouldSucceed_WhenEmailIsNullOrEmpty()
    {
        var user = new ApplicationUser { Id = "1" };
        _userManager.GetEmailAsync(user).Returns((string?)null);
        (await _validator.ValidateAsync(_userManager, user)).Succeeded.Should().BeTrue();

        _userManager.GetEmailAsync(user).Returns("");
        (await _validator.ValidateAsync(_userManager, user)).Succeeded.Should().BeTrue();

        _userManager.GetEmailAsync(user).Returns("   ");
        (await _validator.ValidateAsync(_userManager, user)).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task ValidateAsync_ShouldFail_WhenEmailFormatInvalid()
    {
        var user = new ApplicationUser { Id = "1" };
        _userManager.GetEmailAsync(user).Returns("not-an-email");

        var result = await _validator.ValidateAsync(_userManager, user);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "InvalidEmail");
    }

    [Test]
    public async Task ValidateAsync_ShouldSucceed_WhenEmailIsUnique()
    {
        var user = new ApplicationUser { Id = "1" };
        _userManager.GetEmailAsync(user).Returns("kay@example.com");
        _userManager.FindByEmailAsync("kay@example.com").Returns((ApplicationUser?)null);

        var result = await _validator.ValidateAsync(_userManager, user);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task ValidateAsync_ShouldSucceed_WhenEmailBelongsToSameUser()
    {
        var user = new ApplicationUser { Id = "1" };
        _userManager.GetEmailAsync(user).Returns("kay@example.com");
        _userManager.FindByEmailAsync("kay@example.com").Returns(user);
        _userManager.GetUserIdAsync(user).Returns("1");

        var result = await _validator.ValidateAsync(_userManager, user);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task ValidateAsync_ShouldFail_WhenEmailBelongsToAnotherUser()
    {
        var user = new ApplicationUser { Id = "1" };
        var other = new ApplicationUser { Id = "2" };
        _userManager.GetEmailAsync(user).Returns("kay@example.com");
        _userManager.FindByEmailAsync("kay@example.com").Returns(other);
        _userManager.GetUserIdAsync(other).Returns("2");
        _userManager.GetUserIdAsync(user).Returns("1");

        var result = await _validator.ValidateAsync(_userManager, user);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DuplicateEmail");
    }
}
