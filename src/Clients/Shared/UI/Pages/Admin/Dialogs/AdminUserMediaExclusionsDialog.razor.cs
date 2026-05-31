using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminUserMediaExclusionsDialog
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public List<Guid> ExcludedMediaIds { get; set; } = [];

    private bool _loading = true;
    private List<ExcludedMediaItem> _excludedMedias = [];

    protected override async Task OnInitializedAsync()
    {
        if (ExcludedMediaIds.Count > 0)
        {
            try
            {
                var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
                {
                    Ids = ExcludedMediaIds.ToArray(),
                    PageNumber = 1,
                    PageSize = ExcludedMediaIds.Count
                });

                var fetchedById = result?.Items?.ToDictionary(m => m.Id) ?? [];

                _excludedMedias = ExcludedMediaIds.Select(id =>
                {
                    fetchedById.TryGetValue(id, out var dto);
                    var posterUrl = dto?.Pictures?
                        .FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                        .GetUri(MetadataPictureSize.Small)?.OriginalString;
                    return new ExcludedMediaItem(id, dto?.Title, posterUrl is not null ? ApiClient.GetAbsoluteUri(posterUrl)?.AbsoluteUri : null);
                }).ToList();
            }
            catch
            {
                _excludedMedias = ExcludedMediaIds.Select(id => new ExcludedMediaItem(id, null, null)).ToList();
            }
        }

        _loading = false;
    }

    private void ToggleMedia(ExcludedMediaItem media, bool keepExcluded)
    {
        if (!keepExcluded)
            _excludedMedias.Remove(media);
    }

    private void Cancel() => Dialog.Cancel();

    private void Submit() => Dialog.Close(K7DialogResult.Ok(_excludedMedias.Select(m => m.Id).ToList()));

    private sealed record ExcludedMediaItem(Guid Id, string? Title, string? PosterUrl);
}
