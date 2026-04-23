using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Restrictions;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminRestrictedMediasDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public Guid ProfileId { get; set; }
    [Parameter] public string ProfileName { get; set; } = "";

    private bool _loading = true;
    private List<RestrictedMediaPreviewDto> _medias = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _medias = await K7ServerService.PreviewRestrictedMediasAsync(ProfileId);
        }
        catch
        {
            _medias = [];
        }
        finally
        {
            _loading = false;
        }
    }

    private static string FormatType(MediaType type) => type switch
    {
        MediaType.Movie => "Film",
        MediaType.Serie => "Série",
        MediaType.MusicTrack => "Musique",
        MediaType.MusicAlbum => "Album",
        _ => type.ToString()
    };

    private void Close() => Dialog.Cancel();
}
