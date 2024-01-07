namespace MediaServer.Infrastructure.Context;
public record DatabaseProvider(string Name, string Assembly)
{
    public static DatabaseProvider Postgres = new(nameof(Postgres), "MediaServer.Infrastructure.DatabaseProviders.Postgres");
    public static DatabaseProvider Sqlite = new(nameof(Sqlite), "MediaServer.Infrastructure.DatabaseProviders.Sqlite");
}
