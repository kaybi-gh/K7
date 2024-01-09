namespace MediaServer.Tests.Helpers.Databases;

public static class TestDatabaseFactory
{
    public static async Task<ITestDatabase> CreateAsync()
    {
        var database = new PostgreSqlTestContainerDatabase();
        await database.InitialiseAsync();
        return database;
    }
}
