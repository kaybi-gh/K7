namespace K7.Server.Infrastructure.Database.Context;

public record DatabaseProvider(string Name, string Assembly)
{
    private const string PostgresMigrationsType =
        "K7.Server.Infrastructure.Database.Providers.Postgres.Migrations.ApplicationDbContextModelSnapshot, K7.Server.Infrastructure.Database.Providers.Postgres";

    private const string SqliteMigrationsType =
        "K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations.ApplicationDbContextModelSnapshot, K7.Server.Infrastructure.Database.Providers.Sqlite";

    public static DatabaseProvider Postgres { get; } = new(nameof(Postgres), ResolveAssembly(PostgresMigrationsType));
    public static DatabaseProvider Sqlite { get; } = new(nameof(Sqlite), ResolveAssembly(SqliteMigrationsType));

    private static string ResolveAssembly(string assemblyQualifiedTypeName)
    {
        var type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
        if (type is not null)
        {
            return type.Assembly.GetName().Name!;
        }

        var commaIndex = assemblyQualifiedTypeName.IndexOf(',', StringComparison.Ordinal);
        return commaIndex >= 0
            ? assemblyQualifiedTypeName[(commaIndex + 1)..].Trim()
            : assemblyQualifiedTypeName;
    }
}
