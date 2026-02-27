using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.MAUI.Pages;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;
using K7.Shared.QueryBuilders;

namespace K7.Clients.MAUI.Services;
internal class PlayerService : IPlayerService
{
    public event Func<Task>? PlayRequested;
    public event Func<Task>? PauseRequested;
    public event Func<Task>? StopRequested;
    public event Func<double, Task>? SeekRequested;
    public event Func<Task>? EnterFullScreenRequested;
    public event Func<Task>? ExitFullScreenRequested;
    public event Func<Task>? MuteRequested;
    public event Func<Task>? UnmuteRequest;
    public event Func<double, Task>? VolumeChangeRequested;
    public event Func<double, Task>? PlaybackRateChangeRequested;
    public event Action<int>? SwitchAudioTrackRequested;

#pragma warning disable CS0067
    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;
#pragma warning restore CS0067
    public event Action<bool>? IsFullScreenChanged;
    public event Action<PlaybackState>? PlaybackStateChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? CurrentTimeChanged;
    public event Action<double>? BufferedTimeChanged;
    public event Action<double>? VolumeChanged;
    public event Action<double>? PlaybackRateChanged;
    public event Action<bool>? IsMutedChanged;
    public event Action<AudioFileTrackDto?>? AudioTrackChanged;

    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly IStreamUriService _streamUriService;

    private PlayerPage? _playerPage;
    public PlayerViewModel ViewModel { get; private set; } = new();

    public PlayerService(IK7ServerService k7ServerService, IDeviceStorageService deviceStorageService, IStreamUriService streamUriService)
    {
        _k7ServerService = k7ServerService;
        _deviceStorageService = deviceStorageService;
        _streamUriService = streamUriService;
        ViewModel.MediaElement.StateChanged += MediaElement_StateChanged;

        _volume = _deviceStorageService.Get(PreferenceKeys.PLAYER_VOLUME, 1);
        _playbackRate = _deviceStorageService.Get(PreferenceKeys.PLAYER_PLAYBACK_RATE, 1);
        _isMuted = _deviceStorageService.Get(PreferenceKeys.PLAYER_IS_MUTED, false);
    }

    private IndexedFileDto? _indexedFile;
    public IndexedFileDto? IndexedFile
    {
        get => _indexedFile;
        set => _indexedFile = value;
    }

    private PlayerSource _source = new();
    public PlayerSource Source
    {
        get => _source;
        set
        {
            if (_source != value)
            {
                _source = value;
                CurrentTime = 0;
                Duration = 0;
                BufferedTime = 0;
                PlaybackState = PlaybackState.Idle;
                SourceChanged?.Invoke(value);
            }
        }
    }

    public bool IsVisible { get; private set; } = false;

    private PlaybackState _playbackState = PlaybackState.Unknown;
    public PlaybackState PlaybackState
    {
        get => _playbackState;
        set
        {
            if (_playbackState != value)
            {
                _playbackState = value;
                PlaybackStateChanged?.Invoke(value);
            }
        }
    }

    private bool _isFullScreen = false;
    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (_isFullScreen != value)
            {
                _isFullScreen = value;
                IsFullScreenChanged?.Invoke(value);
            }
        }
    }

    private double _duration = 0;
    public double Duration
    {
        get => _duration;
        set
        {
            if (_duration != value)
            {
                _duration = value;
                DurationChanged?.Invoke(value);
            }
        }
    }

    private double _currentTime = 0;
    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime != value)
            {
                _currentTime = value;
                CurrentTimeChanged?.Invoke(value);
            }
        }
    }

    private double _bufferedTime = 0;
    public double BufferedTime
    {
        get => _bufferedTime;
        set
        {
            if (_bufferedTime != value)
            {
                _bufferedTime = value;
                BufferedTimeChanged?.Invoke(value);
            }
        }
    }

    private double _volume;
    public double Volume
    {
        get => _volume;
        set
        {
            if (_volume != value)
            {
                _volume = value;
                _deviceStorageService.Set(PreferenceKeys.PLAYER_VOLUME, value);
                VolumeChanged?.Invoke(value);
            }
        }
    }

    private double _playbackRate;
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate != value)
            {
                _playbackRate = value;
                _deviceStorageService.Set(PreferenceKeys.PLAYER_PLAYBACK_RATE, value);
                PlaybackRateChanged?.Invoke(value);
            }
        }
    }

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                _deviceStorageService.Set(PreferenceKeys.PLAYER_IS_MUTED, value);
                IsMutedChanged?.Invoke(value);
            }
        }
    }

    private Guid? _currentIndexedFileId;
    private List<AudioFileTrackDto> _audioTracks = [];
    public IReadOnlyList<AudioFileTrackDto> AudioTracks => _audioTracks;

    private AudioFileTrackDto? _selectedAudioTrack;
    public AudioFileTrackDto? SelectedAudioTrack => _selectedAudioTrack;

    public async Task ShowAsync()
    {
        var navigation = Application.Current?.Windows[0]?.Navigation;
        if (navigation == null)
        {
            return;
        }

        _playerPage ??= new PlayerPage(ViewModel);
        if (!navigation.ModalStack.Contains(_playerPage))
        {
            await navigation.PushModalAsync(_playerPage);
        }
    }

    public async Task HideAsync()
    {
        var navigation = Application.Current?.Windows[0]?.Navigation;
        if (navigation == null)
        {
            return;
        }

        if (_playerPage != null && navigation.ModalStack.Contains(_playerPage))
        {
            await navigation.PopModalAsync();
        }
    }



    private void MediaElement_StateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        //_mediaStreamSession.SendPlaybackState()
        var test = "cool";
        Console.WriteLine(test);
    }

    public async Task PlayAsync(IndexedFileDto indexedFile)
    {
        _indexedFile = indexedFile;
        _playerPage ??= new PlayerPage(ViewModel);

        /*if (_indexedFile.Media is MusicTrack musicTrack)
        {
            ViewModel.MediaElement.MetadataArtist = musicTrack.Metadata?.PersonRoles?.FirstOrDefault()?.Person?.Name ?? "";
        }

        ViewModel.MediaElement.MetadataTitle = _indexedFile.Media?.Metadata?.Title ?? "";
        ViewModel.MediaElement.MetadataArtworkUrl = _indexedFile.Media?.Metadata?.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Poster)?.LocalPath ?? "";*/

        var hiddenPlayerService = new HiddenPlayerService();
        var directPlayUri = _k7ServerService.GetAbsoluteUri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id));
        var directPlay = await hiddenPlayerService.TryPlayMediaAsync(MediaSource.FromUri(directPlayUri)!);
        _playerPage.ChangeSource(directPlayUri!.OriginalString);
        //_mediaStreamSession.GetIndexedFileDirectStreamUri()
        var toto = "test";
        Console.WriteLine(toto);
    }

    public void Play() => PlayRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Seek(double time) => SeekRequested?.Invoke(time);
    public void Mute() => MuteRequested?.Invoke();
    public void Unmute() => UnmuteRequest?.Invoke();
    public void SetVolume(double volume) => VolumeChangeRequested?.Invoke(volume);
    public void SetPlaybackRate(double rate) => PlaybackRateChangeRequested?.Invoke(rate);
    public void Stop() => StopRequested?.Invoke();
    public void EnterFullScreen() => EnterFullScreenRequested?.Invoke();
    public void ExitFullScreen() => ExitFullScreenRequested?.Invoke();

    public async Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, int? audioTrackIndex = null, CancellationToken cancellationToken = default)
    {
        _currentIndexedFileId = indexedFileId;
        _audioTracks = audioTracks.ToList();
        _selectedAudioTrack = audioTrackIndex is int idx
            ? _audioTracks.FirstOrDefault(t => t.Index == idx)
            : _audioTracks.FirstOrDefault(t => t.IsDefault) ?? _audioTracks.FirstOrDefault();

        var session = await _streamUriService.GetOrCreateSessionAsync(indexedFileId, cancellationToken: cancellationToken);

        if (session.Source is null)
        {
            throw new InvalidOperationException("Streaming session did not return a source URI.");
        }

        _playerPage ??= new PlayerPage(ViewModel);
        _playerPage.ChangeSource(session.Source.Uri.OriginalString);
        AudioTrackChanged?.Invoke(_selectedAudioTrack);
        await ShowAsync();
        Play();
    }

    public Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default)
    {
        if (_currentIndexedFileId is null)
        {
            return Task.CompletedTask;
        }

        var trackIndex = _audioTracks.IndexOf(track);
        if (trackIndex < 0)
        {
            return Task.CompletedTask;
        }

        _selectedAudioTrack = track;
        AudioTrackChanged?.Invoke(track);
        SwitchAudioTrackRequested?.Invoke(trackIndex);

        return Task.CompletedTask;
    }
}

public class HiddenPlayerService : IDisposable
{
    private readonly MediaElement _mediaElement;
    private TaskCompletionSource<bool>? _mediaIsPlayable;
    private bool _isAttached;

    public HiddenPlayerService()
    {
        _mediaElement = new MediaElement
        {
            IsVisible = true,
            Opacity = 0,
            InputTransparent = true,
            ShouldKeepScreenOn = false,
            ShouldAutoPlay = false,
            ShouldMute = true,
            HeightRequest = 1,
            WidthRequest = 1
        };

        _mediaElement.MediaOpened += (s, e) => _mediaIsPlayable?.TrySetResult(true);
        _mediaElement.MediaFailed += (s, e) => _mediaIsPlayable?.TrySetResult(false);
        _mediaElement.MediaFailed += (s, e) => Test(e);
    }

    private void Test(MediaFailedEventArgs e)
    {
        var toto = e.ErrorMessage;
    }

    private void EnsureAttachedToVisualTree()
    {
        if (_isAttached)
            return;

        var page = Application.Current?.Windows[0].Page;
        if (page is not NavigationPage navigationPage)
            return;

        if (navigationPage.CurrentPage is not ContentPage contentPage)
            return;

        if (contentPage.Content is Layout layout)
        {
            layout.Children.Add(_mediaElement);
            _isAttached = true;
        }
        else if (contentPage.Content is View view)
        {
            // Remplacer Content par une Grid qui contient l'ancien contenu + player
            var grid = new Grid();
            grid.Children.Add(view);
            grid.Children.Add(_mediaElement);
            contentPage.Content = grid;
            _isAttached = true;
        }
    }

    public async Task<bool> TryPlayMediaAsync(MediaSource mediaSource, int timeoutMs = 15000)
    {
        EnsureAttachedToVisualTree();
        _mediaIsPlayable = new TaskCompletionSource<bool>();

        _mediaElement.Source = mediaSource;
        _mediaElement.Play();

        var completedTask = await Task.WhenAny(_mediaIsPlayable.Task, Task.Delay(timeoutMs));

        if (completedTask != _mediaIsPlayable.Task)
        {
            return false;
        }

        return _mediaIsPlayable.Task.Result;
    }

    public void Dispose()
    {
        _mediaElement?.Dispose();
        _mediaIsPlayable = null;
    }
}
