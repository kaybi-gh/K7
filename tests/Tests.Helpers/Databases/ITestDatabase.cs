using System.Data.Common;

namespace K7.Tests.Helpers.Databases;

public interface ITestDatabase
{
    Task InitializeAsync();

    DbConnection GetConnection();

    string GetConnectionString();

    Task ResetAsync();

    Task DisposeAsync();
}
