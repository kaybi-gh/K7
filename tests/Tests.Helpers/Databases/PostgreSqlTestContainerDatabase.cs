using System.Data.Common;
using MediaServer.Infrastructure.Context;
using MediaServer.Infrastructure.Context.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace MediaServer.Tests.Helpers.Databases;

public class PostgreSqlTestContainerDatabase : ITestDatabase
{
    private readonly PostgreSqlContainer _container;
    private NpgsqlConnection _connection = null!;
    private string _connectionString = null!;
    private Respawner _respawner = null!;

    public PostgreSqlTestContainerDatabase()
    {
        _container = new PostgreSqlBuilder()
            .WithAutoRemove(true)
            .Build();
    }

    public async Task InitialiseAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, x => x.MigrationsAssembly(DatabaseProvider.Postgres.Assembly))
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.Migrate();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            SchemasToInclude =
            [
                "public"
            ],
            TablesToIgnore = ["__EFMigrationsHistory"],
            DbAdapter = DbAdapter.Postgres
        });
    }

    public DbConnection GetConnection()
    {
        return _connection;
    }

    public async Task ResetAsync()
    {

        await _respawner.ResetAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}
