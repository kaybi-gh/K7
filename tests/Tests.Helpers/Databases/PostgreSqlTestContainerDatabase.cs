using System.Data.Common;
using K7.Server.Application.Common.Configuration;
using K7.Server.Infrastructure.Database.Context;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace K7.Tests.Helpers.Databases;

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

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, x => x.MigrationsAssembly(DatabaseProvider.Postgres.Assembly))
            .UseOpenIddict()
            .Options;

        var context = new ApplicationDbContext(options,
            Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() }));
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
