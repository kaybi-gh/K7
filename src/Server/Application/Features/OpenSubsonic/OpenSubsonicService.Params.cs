using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Application.Features.Medias.Commands.RateMedia;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;
using K7.Server.Application.Features.Medias.Queries.GetSimilarMusicArtists;
using K7.Server.Application.Features.MusicIntelligence.Queries.GetSimilarTracks;
using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Application.Features.Playlists.Commands.DeletePlaylist;
using K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService
{
    private static string? GetParam(IReadOnlyDictionary<string, string[]> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var values) || values.Length == 0)
            return null;
        return values[0];
    }

    private static IReadOnlyList<string> GetParams(IReadOnlyDictionary<string, string[]> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var values) || values.Length == 0)
            return [];
        return values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();
    }

    private static Guid? GetGuid(IReadOnlyDictionary<string, string[]> parameters, string key)
    {
        var value = GetParam(parameters, key);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static List<Guid> GetGuids(IReadOnlyDictionary<string, string[]> parameters, string key) =>
        GetParams(parameters, key)
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();

    private static int GetInt(
        IReadOnlyDictionary<string, string[]> parameters,
        string key,
        int defaultValue,
        int min,
        int max)
    {
        var value = GetParam(parameters, key);
        if (!int.TryParse(value, out var parsed))
            return defaultValue;
        return Math.Clamp(parsed, min, max);
    }

    private static int? GetNullableInt(IReadOnlyDictionary<string, string[]> parameters, string key)
    {
        var value = GetParam(parameters, key);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool? GetBool(IReadOnlyDictionary<string, string[]> parameters, string key)
    {
        var value = GetParam(parameters, key);
        if (value is null)
            return null;
        if (bool.TryParse(value, out var parsed))
            return parsed;
        if (value is "1" or "true" or "True")
            return true;
        if (value is "0" or "false" or "False")
            return false;
        return null;
    }

    private static double? GetDouble(IReadOnlyDictionary<string, string[]> parameters, string key)
    {
        var value = GetParam(parameters, key);
        if (value is null)
            return null;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
