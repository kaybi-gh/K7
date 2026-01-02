using System.ComponentModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace K7.Clients.MAUI.Pages;

public partial class PlayerPage : ContentPage
{
	public PlayerPage(PlayerViewModel viewModel)
	{
        BindingContext = viewModel;
		InitializeComponent();
    }

    public void OnPlayPauseButtonClicked(object sender, EventArgs args)
    {
        if (player.CurrentState == MediaElementState.Stopped ||
            player.CurrentState == MediaElementState.Paused)
        {
            player.Play();
        }
        else if (player.CurrentState == MediaElementState.Playing)
        {
            player.Pause();
        }
    }

    public void OnStopButtonClicked(object sender, EventArgs args)
    {
        player.Stop();
    }

    public void OnPlayerPageUnloaded(object? sender, EventArgs e)
    {
        // Stop and cleanup MediaElement when we navigate away
        player.Handler?.DisconnectHandler();
    }

    public async void OnCloseClicked(object sender, EventArgs e)
    {
        player.Handler?.DisconnectHandler();
        await Navigation.PopModalAsync();
    }

    public void ChangeSource(string source)
    {
        player.Source = MediaSource.FromUri(source);
    }

    internal void ChangeArtwork(string poster)
    {
        player.MetadataArtworkUrl = poster;
    }
}
public class PlayerViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private MediaElement _mediaElement = new();
    public MediaElement MediaElement
    {
        get => _mediaElement;
        set
        {
            _mediaElement = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MediaElement)));
        }
    }
}
