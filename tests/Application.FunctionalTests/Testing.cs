using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests;

[SetUpFixture]
public class Testing
{
    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        await DatabaseFixture.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task RunAfterAllTests()
    {
        await DatabaseFixture.DisposeAsync();
    }
}
