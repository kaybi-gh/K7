using System.Globalization;
using System.Text;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Services;

public static class SupplementalSerieCastEnricher
{
    public sealed record EnrichmentResult(
        IReadOnlyList<BasePersonRole> RolesToAppend,
        IReadOnlyList<string> UnresolvedTvdbPeopleIds);

    public static EnrichmentResult Enrich(
        IList<BasePersonRole> primaryRoles,
        IReadOnlyList<BasePersonRole> supplementalRoles)
    {
        if (supplementalRoles.Count == 0)
        {
            return new EnrichmentResult(
                [],
                CollectUnresolvedTvdbPeopleIds(primaryRoles));
        }

        var matchedSupplemental = new HashSet<BasePersonRole>();

        foreach (var primary in primaryRoles)
        {
            var match = FindBestMatch(primary, supplementalRoles, matchedSupplemental);
            if (match is null)
                continue;

            matchedSupplemental.Add(match);
            PersonMetadataMergeHelper.MergeMissingPersonData(primary.Person, match.Person);

            if (primary.PortraitPicture is null && match.PortraitPicture?.OriginalRemoteUri is not null)
            {
                primary.PortraitPicture = new MetadataPicture
                {
                    OriginalRemoteUri = match.PortraitPicture.OriginalRemoteUri,
                    Type = MetadataPictureType.Portrait
                };
            }
        }

        var rolesToAppend = supplementalRoles
            .Where(role => !matchedSupplemental.Contains(role))
            .Where(role => !HasEquivalentRole(primaryRoles, role))
            .ToList();

        return new EnrichmentResult(
            rolesToAppend,
            CollectUnresolvedTvdbPeopleIds(primaryRoles));
    }

    public static IReadOnlyList<string> CollectUnresolvedTvdbPeopleIds(IEnumerable<BasePersonRole> roles) =>
        roles
            .Select(r => r.Person)
            .Distinct()
            .Where(p => p.ExternalIds.Any(e => e.ProviderName == "tvdb")
                && !p.ExternalIds.Any(e => e.ProviderName is "tmdb" or "imdb"))
            .Select(p => p.ExternalIds.First(e => e.ProviderName == "tvdb").Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList()!;

    private static BasePersonRole? FindBestMatch(
        BasePersonRole primary,
        IReadOnlyList<BasePersonRole> supplementalRoles,
        HashSet<BasePersonRole> alreadyMatched)
    {
        var candidates = supplementalRoles
            .Where(r => !alreadyMatched.Contains(r) && r.Type == primary.Type)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var primaryTmdb = GetExternalId(primary.Person, "tmdb");
        if (primaryTmdb is not null)
        {
            var byTmdb = candidates.FirstOrDefault(c =>
                string.Equals(GetExternalId(c.Person, "tmdb"), primaryTmdb, StringComparison.OrdinalIgnoreCase));
            if (byTmdb is not null)
                return byTmdb;
        }

        var primaryImdb = GetExternalId(primary.Person, "imdb");
        if (primaryImdb is not null)
        {
            var byImdb = candidates.FirstOrDefault(c =>
                string.Equals(GetExternalId(c.Person, "imdb"), primaryImdb, StringComparison.OrdinalIgnoreCase));
            if (byImdb is not null)
                return byImdb;
        }

        var primaryPersonName = Normalize(primary.Person.Name);
        var primaryCharacter = primary is Actor primaryActor
            ? Normalize(primaryActor.CharacterName)
            : null;

        if (!string.IsNullOrEmpty(primaryPersonName) && !string.IsNullOrEmpty(primaryCharacter))
        {
            var byBoth = candidates
                .Where(c => c is Actor actor
                    && Normalize(c.Person.Name) == primaryPersonName
                    && Normalize(actor.CharacterName) == primaryCharacter)
                .ToList();
            if (byBoth.Count == 1)
                return byBoth[0];
        }

        if (!string.IsNullOrEmpty(primaryCharacter) && primary.Type == PersonRoleType.Actor)
        {
            var byCharacter = candidates
                .OfType<Actor>()
                .Where(c => Normalize(c.CharacterName) == primaryCharacter)
                .Cast<BasePersonRole>()
                .ToList();
            if (byCharacter.Count == 1)
                return byCharacter[0];
        }

        if (!string.IsNullOrEmpty(primaryPersonName))
        {
            var byName = candidates
                .Where(c => Normalize(c.Person.Name) == primaryPersonName)
                .ToList();
            if (byName.Count == 1)
                return byName[0];
        }

        if (primary is CrewMember primaryCrew
            && !string.IsNullOrEmpty(primaryPersonName))
        {
            var byCrew = candidates
                .OfType<CrewMember>()
                .Where(c => Normalize(c.Person.Name) == primaryPersonName
                    && string.Equals(c.Job, primaryCrew.Job, StringComparison.OrdinalIgnoreCase))
                .Cast<BasePersonRole>()
                .ToList();
            if (byCrew.Count == 1)
                return byCrew[0];
        }

        return null;
    }

    private static bool HasEquivalentRole(IEnumerable<BasePersonRole> existingRoles, BasePersonRole candidate)
    {
        var candidateTmdbCredit = GetExternalId(candidate, "tmdb");
        if (candidateTmdbCredit is not null
            && existingRoles.Any(r => string.Equals(GetExternalId(r, "tmdb"), candidateTmdbCredit, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var candidatePersonTmdb = GetExternalId(candidate.Person, "tmdb");
        if (candidatePersonTmdb is not null
            && existingRoles.Any(r =>
                r.Type == candidate.Type
                && string.Equals(GetExternalId(r.Person, "tmdb"), candidatePersonTmdb, StringComparison.OrdinalIgnoreCase)
                && RolesLookSame(r, candidate)))
        {
            return true;
        }

        return existingRoles.Any(r =>
            r.Type == candidate.Type
            && Normalize(r.Person.Name) == Normalize(candidate.Person.Name)
            && RolesLookSame(r, candidate));
    }

    private static bool RolesLookSame(BasePersonRole left, BasePersonRole right)
    {
        if (left is Actor leftActor && right is Actor rightActor)
            return Normalize(leftActor.CharacterName) == Normalize(rightActor.CharacterName);

        if (left is CrewMember leftCrew && right is CrewMember rightCrew)
        {
            return string.Equals(leftCrew.Job, rightCrew.Job, StringComparison.OrdinalIgnoreCase)
                && string.Equals(leftCrew.Department, rightCrew.Department, StringComparison.OrdinalIgnoreCase);
        }

        return left.Type == right.Type;
    }

    private static string? GetExternalId(Person person, string providerName) =>
        person.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? GetExternalId(BasePersonRole role, string providerName) =>
        role.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))?.Value;

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim().ToLowerInvariant();
        var normalized = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch == '-')
                sb.Append(' ');
            // Apostrophes and similar are dropped so O'Neil -> oneil
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
