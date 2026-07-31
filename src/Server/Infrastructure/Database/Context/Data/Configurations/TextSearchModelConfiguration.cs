using System.Reflection;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

internal static class TextSearchModelConfiguration
{
    private static readonly MethodInfo ILikeMethod =
        typeof(EfLikeQueryExtensions).GetMethod(
            nameof(EfLikeQueryExtensions.ILike),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(string)])
        ?? throw new InvalidOperationException($"Could not find {nameof(EfLikeQueryExtensions.ILike)}.");

    private static readonly Type? PgILikeExpressionType = Type.GetType(
        "Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgILikeExpression, Npgsql.EntityFrameworkCore.PostgreSQL");

    public static void Configure(ModelBuilder builder, bool isPostgres)
    {
        if (isPostgres)
        {
            ConfigurePostgres(builder);
        }
        else
        {
            ConfigureSqlite(builder);
        }
    }

    private static void ConfigurePostgres(ModelBuilder builder)
    {
        builder.HasPostgresExtension("pg_trgm");

        builder.HasDbFunction(ILikeMethod)
            .HasTranslation(args => CreatePgILike(args[0], args[1]));

        builder.Entity<BaseMedia>()
            .HasIndex(e => e.Title)
            .HasDatabaseName("IX_Medias_Title_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasFilter("\"Title\" IS NOT NULL");

        builder.Entity<Person>()
            .HasIndex(e => e.Name)
            .HasDatabaseName("IX_Persons_Name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.Entity<Actor>()
            .HasIndex(e => e.CharacterName)
            .HasDatabaseName("IX_PersonRoles_CharacterName_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasFilter("\"CharacterName\" IS NOT NULL");

        builder.Entity<VoiceActor>()
            .HasIndex(e => e.CharacterName)
            .HasDatabaseName("IX_PersonRoles_VoiceActor_CharacterName_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasFilter("\"VoiceActor_CharacterName\" IS NOT NULL");
    }

    private static void ConfigureSqlite(ModelBuilder builder)
    {
        builder.HasDbFunction(ILikeMethod)
            .HasTranslation(args =>
            {
                var match = args[0];
                var lowered = new SqlFunctionExpression(
                    "lower",
                    [match],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(string),
                    match.TypeMapping);

                return new LikeExpression(
                    lowered,
                    args[1],
                    escapeChar: null,
                    typeMapping: new BoolTypeMapping("INTEGER"));
            });
    }

    private static SqlExpression CreatePgILike(SqlExpression match, SqlExpression pattern)
    {
        if (PgILikeExpressionType is null)
            throw new InvalidOperationException("Npgsql PgILikeExpression type was not found.");

        return (SqlExpression)Activator.CreateInstance(
            PgILikeExpressionType,
            match,
            pattern,
            null,
            new BoolTypeMapping("boolean"))!;
    }
}
