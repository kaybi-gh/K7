using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminUserMediaExclusionsDialog
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
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

    private void RemoveExclusion(ExcludedMediaItem media)
    {
        _excludedMedias.Remove(media);
    }

    private void Cancel() => MudDialog.Cancel();

    private void Submit() => MudDialog.Close(DialogResult.Ok(_excludedMedias.Select(m => m.Id).ToList()));

    private sealed record ExcludedMediaItem(Guid Id, string? Title, string? PosterUrl);
}
