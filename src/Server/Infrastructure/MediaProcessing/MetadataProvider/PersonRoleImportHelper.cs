using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

internal static class PersonRoleImportHelper
{
    internal static void DedupByTmdbCreditId(IList<BasePersonRole> roles)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = roles.Count - 1; i >= 0; i--)
        {
            var role = roles[i];
            var tmdbId = role.ExternalIds.FirstOrDefault(e => e.ProviderName == "tmdb")?.Value;
            if (tmdbId is null)
                continue;

            var key = $"{role.Type}:{tmdbId}";
            if (!seenKeys.Add(key))
                roles.RemoveAt(i);
        }
    }
}
