namespace K7.Server.Infrastructure.Context;
public record DatabaseProvider(string Name, string Assembly)
{
    public static DatabaseProvider Postgres = new(nameof(Postgres), "K7.Server.Infrastructure.DatabaseProviders.Postgres");
    public static DatabaseProvider Sqlite = new(nameof(Sqlite), "K7.Server.Infrastructure.DatabaseProviders.Sqlite");
}
