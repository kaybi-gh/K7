using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Application.Common.QueryExtensions;

public static class TextSearchFunctions
{
    public const string FoldDiacriticsFunctionName = "k7_fold_diacritics";

    /// <summary>
    /// Strips combining marks in SQL. Translates to k7_fold_diacritics.
    /// Do not call outside of expression trees.
    /// </summary>
    public static string FoldDiacritics(string value)
        => throw new InvalidOperationException($"{nameof(FoldDiacritics)} can only be used in EF Core queries.");

    public static IQueryable<Person> WhereNameMatches(
        this IQueryable<Person> query,
        string pattern,
        string foldedPattern) =>
        query.Where(person =>
            EfLikeQueryExtensions.ILike(person.Name, pattern)
            || EfLikeQueryExtensions.ILike(FoldDiacritics(person.Name), foldedPattern));

    public static IQueryable<Actor> WhereCharacterNameMatches(
        this IQueryable<Actor> query,
        string pattern,
        string foldedPattern) =>
        query.Where(role =>
            EfLikeQueryExtensions.ILike(role.CharacterName, pattern)
            || EfLikeQueryExtensions.ILike(FoldDiacritics(role.CharacterName), foldedPattern));

    public static IQueryable<VoiceActor> WhereCharacterNameMatches(
        this IQueryable<VoiceActor> query,
        string pattern,
        string foldedPattern) =>
        query.Where(role =>
            EfLikeQueryExtensions.ILike(role.CharacterName, pattern)
            || EfLikeQueryExtensions.ILike(FoldDiacritics(role.CharacterName), foldedPattern));
}
