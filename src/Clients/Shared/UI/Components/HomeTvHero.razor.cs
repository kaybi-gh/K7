using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class HomeTvHero
{
    private readonly string?[] _layerUrls = [null, null];
    private int _activeLayer;

    [Parameter] public MediaCardViewModel? Model { get; set; }

    protected override void OnParametersSet()
    {
        var newUrl = Model?.BackdropUrl ?? Model?.PictureUrl;
        if (newUrl == _layerUrls[_activeLayer])
            return;

        // Put the new image on the inactive layer, then make it active
        var inactiveLayer = 1 - _activeLayer;
        _layerUrls[inactiveLayer] = newUrl;
        _activeLayer = inactiveLayer;
    }

    private static string FormatRuntime(int minutes)
    {
        if (minutes >= 60)
            return $"{minutes / 60}h {minutes % 60}min";
        return $"{minutes}min";
    }

    private static string TruncateOverview(string text, int maxLength = 200)
    {
        if (text.Length <= maxLength)
            return text;
        var truncated = text[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
            truncated = truncated[..lastSpace];
        return truncated + "...";
    }
}
