namespace MediaServer.Application.FunctionalTests.Fixtures;

[TestFixture]
public abstract class BaseTestFixture : Testing
{
    [SetUp]
    public async Task TestSetUp()
    {
        await ResetState();
    }
}
