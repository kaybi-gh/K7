using FluentAssertions;
using K7.Server.Application.Common.Security;
using NUnit.Framework;

namespace K7.Server.Application.UnitTests.Features.ViewingGroups;

public class PinHashHelperTests
{
    [Test]
    public void Verify_ShouldReturnTrue_ForMatchingPin()
    {
        var hash = PinHashHelper.Hash("1234");

        PinHashHelper.Verify(hash, "1234").Should().BeTrue();
    }

    [Test]
    public void Verify_ShouldReturnFalse_ForWrongPin()
    {
        var hash = PinHashHelper.Hash("1234");

        PinHashHelper.Verify(hash, "9999").Should().BeFalse();
    }
}
