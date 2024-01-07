using System.Data.Common;

namespace MediaServer.Application.FunctionalTests.Fixtures;

public interface ITestDatabase
{
    Task InitialiseAsync();

    DbConnection GetConnection();

    Task ResetAsync();

    Task DisposeAsync();
}
