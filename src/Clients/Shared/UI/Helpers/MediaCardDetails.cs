using System.Globalization;
using K7.Clients.Shared.Models;
using K7.Shared.Enums;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// The extra line of information under a card: the fields the user picked, in a fixed order,
/// keeping only the ones the item has.
/// </summary>
public static class MediaCardDetails
{
    /// <summary>Separator between two details (a middle dot).</summary>
    public const string Separator = " · ";

    /// <summary>Resource keys the localizer must provide, with hours and minutes as arguments.</summary>
    public const string HoursMinutesKey = "DetailHoursMinutes";

    public const string MinutesKey = "DetailMinutes";

    /// <summary>
    /// Builds the line, or null when there is nothing to show. A year the subtitle already shows
    /// is left out.
    /// </summary>
    public static string? Build(
        MediaCardViewModel? model,
        IReadOnlyList<CardDetailField>? fields,
        string? subtitle,
        IStringLocalizer localizer)
    {
        var parts = Parts(model, fields, subtitle, localizer);
        return parts.Count == 0 ? null : string.Join(Separator, parts);
    }

    /// <summary>The details one by one, for badges. Same order and rules as <see cref="Build"/>.</summary>
    public static IReadOnlyList<string> Parts(
        MediaCardViewModel? model,
        IReadOnlyList<CardDetailField>? fields,
        string? subtitle,
        IStringLocalizer localizer)
    {
        if (model is null || fields is not { Count: > 0 })
            return [];

        return Enum.GetValues<CardDetailField>()
            .Where(fields.Contains)
            .Select(field => Format(model, field, subtitle, localizer))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!)
            .ToList();
    }

    private static string? Format(
        MediaCardViewModel model,
        CardDetailField field,
        string? subtitle,
        IStringLocalizer localizer) => field switch
        {
            CardDetailField.Year => FormatYear(model.ReleaseYear, subtitle),
            CardDetailField.Runtime => FormatRuntime(model.RuntimeMinutes, localizer),
            CardDetailField.Rating => model.Rating is > 0
                ? model.Rating.Value.ToString("0.#", CultureInfo.CurrentCulture) + "/10"
                : null,
            CardDetailField.ContentRating => model.ContentRating,
            CardDetailField.Genre => model.Genres?.FirstOrDefault(),
            _ => null
        };

    private static string? FormatYear(int? year, string? subtitle)
    {
        var text = year?.ToString(CultureInfo.InvariantCulture);
        return string.Equals(text, subtitle?.Trim(), StringComparison.Ordinal) ? null : text;
    }

    private static string? FormatRuntime(int? minutes, IStringLocalizer localizer)
    {
        if (minutes is not > 0)
            return null;

        return minutes.Value >= 60
            ? localizer[HoursMinutesKey, minutes.Value / 60, minutes.Value % 60]
            : localizer[MinutesKey, minutes.Value];
    }
}
