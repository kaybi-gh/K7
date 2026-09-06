using K7.Shared.Security;

namespace K7.Server.Application.UnitTests.Security;

[TestFixture]
public class EmailFormatTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void IsValidOptional_ShouldAcceptEmpty(string? email)
    {
        EmailFormat.IsValidOptional(email).Should().BeTrue();
    }

    [TestCase("user@example.com")]
    [TestCase("first.last+tag@mail.example.org")]
    public void IsValidOptional_ShouldAcceptTypicalAddresses(string email)
    {
        EmailFormat.IsValidOptional(email).Should().BeTrue();
        EmailFormat.IsValidRequired(email).Should().BeTrue();
    }

    [TestCase("not-an-email")]
    [TestCase("user@")]
    [TestCase("@example.com")]
    [TestCase("user@localhost")]
    [TestCase("user example@example.com")]
    public void IsValidRequired_ShouldRejectMalformedAddresses(string email)
    {
        EmailFormat.IsValidRequired(email).Should().BeFalse();
        EmailFormat.IsValidOptional(email).Should().BeFalse();
    }
}
