using System.Data.Common;

namespace MediaServer.Tests.Helpers.Databases;

public interface ITestDatabase
{
    Task InitialiseAsync();

    DbConnection GetConnection();

    Task ResetAsync();

    Task DisposeAsync();
}
