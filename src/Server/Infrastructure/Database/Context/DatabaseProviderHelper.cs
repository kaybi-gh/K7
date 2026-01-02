namespace K7.Server.Infrastructure.Database.Context;
public record DatabaseProvider(string Name, string Assembly)
{
    public static DatabaseProvider Postgres = new(nameof(Postgres), "K7.Server.Infrastructure.Database.Providers.Postgres"); // TODO - Use pointer and typeof
    public static DatabaseProvider Sqlite = new(nameof(Sqlite), "K7.Server.Infrastructure.Database.Providers.Sqlite"); // TODO - Use pointer and typeof
}
