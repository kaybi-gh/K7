using CarPlay;
using Foundation;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Interfaces;
using UIKit;

namespace K7.Clients.MAUI.Platforms.iOS.CarPlay;

/// <summary>
/// CarPlay scene delegate that provides a browsable media tree and now-playing interface.
/// Requires the com.apple.developer.carplay-audio entitlement (Apple approval needed).
/// </summary>
[Register("CarPlaySceneDelegate")]
public class CarPlaySceneDelegate : UIResponder, ICPTemplateApplicationSceneDelegate
{
    private CPInterfaceController? _interfaceController;
    private IMediaBrowseService? _mediaBrowseService;
    private IAudioPlayerService? _audioPlayerService;

    [Export("templateApplicationScene:didConnectInterfaceController:")]
    public void DidConnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController)
    {
        _interfaceController = interfaceController;

        var services = IPlatformApplication.Current?.Services;
        if (services is null) return;

        _mediaBrowseService = services.GetRequiredService<IMediaBrowseService>();
        _audioPlayerService = services.GetRequiredService<IAudioPlayerService>();

        _ = BuildAndSetRootTemplateAsync();
    }

    [Export("templateApplicationScene:didDisconnectInterfaceController:")]
    public void DidDisconnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController)
    {
        _interfaceController = null;
    }

    private async Task BuildAndSetRootTemplateAsync()
    {
        if (_mediaBrowseService is null || _interfaceController is null) return;

        try
        {
            var rootItems = await _mediaBrowseService.GetRootItemsAsync();
            var listItems = rootItems.Select(item => CreateListItem(item)).ToArray();

            var section = new CPListSection(listItems.Cast<ICPListTemplateItem>().ToArray(), "Library", "Browse your music");
            var listTemplate = new CPListTemplate("K7", new[] { section });

            var tabTemplate = new CPTabBarTemplate(new CPTemplate[] { listTemplate, CPNowPlayingTemplate.SharedTemplate });

            _interfaceController.SetRootTemplate(tabTemplate, true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CarPlay] Failed to build root: {ex.Message}");
        }
    }

    private CPListItem CreateListItem(MediaBrowseItem item)
    {
        var listItem = new CPListItem(item.Title, item.Subtitle);
        listItem.AccessoryType = item.IsBrowsable
            ? CPListItemAccessoryType.DisclosureIndicator
            : CPListItemAccessoryType.None;

        listItem.Handler = async (cpItem, completion) =>
        {
            if (item.IsPlayable)
            {
                await PlayItemAsync(item);
                completion();
            }
            else if (item.IsBrowsable)
            {
                await PushChildrenAsync(item);
                completion();
            }
            else
            {
                completion();
            }
        };

        if (item.ArtworkUrl is not null)
            _ = LoadArtworkForItemAsync(listItem, item.ArtworkUrl);

        return listItem;
    }

    private async Task PushChildrenAsync(MediaBrowseItem parentItem)
    {
        if (_mediaBrowseService is null || _interfaceController is null) return;

        try
        {
            var children = await _mediaBrowseService.GetChildrenAsync(parentItem.Id);
            var listItems = children.Select(item => CreateListItem(item)).ToArray();

            var section = new CPListSection(listItems.Cast<ICPListTemplateItem>().ToArray(), parentItem.Title, null);
            var childTemplate = new CPListTemplate(parentItem.Title, new[] { section });

            _interfaceController.PushTemplate(childTemplate, true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CarPlay] Failed to load children: {ex.Message}");
        }
    }

    private async Task PlayItemAsync(MediaBrowseItem item)
    {
        if (_mediaBrowseService is null || _audioPlayerService is null) return;

        try
        {
            var tracks = await _mediaBrowseService.GetPlayableItemsAsync(item.Id);
            if (tracks.Count > 0)
            {
                await _audioPlayerService.PlayTracksAsync(tracks, 0);

                // Navigate to Now Playing
                _interfaceController?.PushTemplate(CPNowPlayingTemplate.SharedTemplate, true, null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CarPlay] Failed to play item: {ex.Message}");
        }
    }

    private async Task LoadArtworkForItemAsync(CPListItem listItem, string artworkUrl)
    {
        try
        {
            var k7Server = IPlatformApplication.Current?.Services?.GetService<IK7ServerService>();
            var absoluteUri = k7Server?.GetAbsoluteUri(artworkUrl);
            if (absoluteUri is null) return;

            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(absoluteUri);
            var nsData = NSData.FromArray(data);
            var image = UIKit.UIImage.LoadFromData(nsData);

            if (image is not null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    listItem.SetImage(image);
                });
            }
        }
        catch
        {
            // Artwork is optional, ignore failures
        }
    }
}
