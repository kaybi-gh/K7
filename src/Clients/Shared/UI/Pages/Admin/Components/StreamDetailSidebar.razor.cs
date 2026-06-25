using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class StreamDetailSidebar : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired]
    public StreamDetailModel Model { get; set; } = default!;

    [Parameter]
    public EventCallback OnClose { get; set; }

    private ElementReference _backdrop;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _backdrop.FocusAsync();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await OnClose.InvokeAsync();
        }
    }

    public void Dispose()
    {
        // No-op, required by IDisposable pattern
    }
}

public sealed record StreamDetailModel
{
    public string? MediaTitle { get; init; }
    public string? MediaType { get; init; }
    public string? Status { get; init; }
    public string? StatusVariant { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public string? DurationDisplay { get; init; }
    public string? Position { get; init; }

    public string? UserName { get; init; }
    public string? DeviceName { get; init; }
    public string? DeviceClient { get; init; }

    public bool HasStreamDetails { get; init; }
    public string? ModeLabel { get; init; }
    public string? ModeBadgeVariant { get; init; }
    public string? VideoDecision { get; init; }
    public string? AudioDecision { get; init; }
    public string? SourceVideoCodec { get; init; }
    public string? SourceAudioCodec { get; init; }
    public string? StreamVideoCodec { get; init; }
    public string? StreamAudioCodec { get; init; }
    public string? Resolution { get; init; }
    public string? TranscodeReason { get; init; }
    public string? Bitrate { get; init; }

    public string? AudioTrackLanguage { get; init; }
    public string? AudioTrackTitle { get; init; }
    public string? AudioChannelLayout { get; init; }
    public string? SubtitleTrackLanguage { get; init; }
    public string? SubtitleTrackTitle { get; init; }
    public bool IsSubtitleBurnIn { get; init; }
}
