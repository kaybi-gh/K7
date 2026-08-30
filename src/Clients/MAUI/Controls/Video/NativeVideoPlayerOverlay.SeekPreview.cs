using K7.Clients.Shared.Helpers;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Seek preview HUD: cropped sprite-sheet thumbnail (Skia) + chapter title + time,
/// positioned above the seek thumb like Blazor SeekBar <c>.thumbnail</c>.
/// </summary>
public sealed partial class NativeVideoPlayerOverlay
{
    private readonly Border _seekPreview = new();
    private readonly Border _seekPreviewImageBorder = new();
    private readonly Image _seekPreviewImage = new();
    private readonly Label _seekPreviewTime = new();
    private readonly Label _seekPreviewChapterTitle = new();

    private SKBitmap? _spriteBitmap;
    private string? _spriteLoadedUrl;
    private string? _spriteLoadingUrl;
    private Task? _spriteLoadTask;
    private int _lastSpriteIndex = -1;
    private int _cropVersion;
    private CancellationTokenSource? _cropDebounceCts;

    private void BuildSeekPreview()
    {
        _seekPreviewChapterTitle.TextColor = Colors.White;
        _seekPreviewChapterTitle.FontSize = 12;
        _seekPreviewChapterTitle.FontAttributes = FontAttributes.Bold;
        _seekPreviewChapterTitle.HorizontalTextAlignment = TextAlignment.Center;
        _seekPreviewChapterTitle.IsVisible = false;

        _seekPreviewTime.TextColor = Colors.White;
        _seekPreviewTime.FontSize = 13;
        _seekPreviewTime.HorizontalTextAlignment = TextAlignment.Center;
        _seekPreviewTime.Margin = new Thickness(0, 4, 0, 0);

        _seekPreviewImage.WidthRequest = NativeSeekThumbnailHelper.ThumbWidth / 2.0;
        _seekPreviewImage.HeightRequest = NativeSeekThumbnailHelper.ThumbHeight / 2.0;
        _seekPreviewImage.Aspect = Aspect.AspectFill;

        _seekPreviewImageBorder.Content = _seekPreviewImage;
        _seekPreviewImageBorder.Stroke = Colors.White;
        _seekPreviewImageBorder.StrokeThickness = 1;
        _seekPreviewImageBorder.StrokeShape = new RoundRectangle { CornerRadius = 4 };
        _seekPreviewImageBorder.Padding = 0;
        _seekPreviewImageBorder.IsVisible = false;

        var stack = new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 4,
            Children = { _seekPreviewImageBorder, _seekPreviewChapterTitle, _seekPreviewTime }
        };

        _seekPreview.Content = stack;
        _seekPreview.BackgroundColor = Color.FromArgb("#B3000000");
        _seekPreview.Stroke = Colors.Transparent;
        _seekPreview.StrokeShape = new RoundRectangle { CornerRadius = 8 };
        _seekPreview.Padding = new Thickness(8);
        _seekPreview.IsVisible = false;
        _seekPreview.Opacity = 0;
        // Stable size so the first PositionSeekPreview is not glued to the bar then jumping.
        _seekPreview.MinimumWidthRequest = 160;
        _seekPreview.HorizontalOptions = LayoutOptions.Start;
        _seekPreview.VerticalOptions = LayoutOptions.Start;
        _seekPreview.InputTransparent = true;
        _seekPreview.SizeChanged += (_, _) =>
        {
            if (!_seekPreview.IsVisible || _seekPreview.Width <= 1 || _seekPreview.Height <= 1)
                return;
            PositionSeekPreview(_seekBar.DisplayTime);
            _seekPreview.Opacity = 1;
        };
        Children.Add(_seekPreview);
    }

    private void UpdateSeekPreview(bool show)
    {
        if (!show)
        {
            _seekPreview.IsVisible = false;
            _seekPreview.Opacity = 0;
            _cropDebounceCts?.Cancel();
            return;
        }

        var time = _seekBar.DisplayTime;
        _seekPreviewTime.Text = NativeTimeFormatting.Format(time);

        var chapterTitle = GetHoveredChapterTitle(time);
        _seekPreviewChapterTitle.IsVisible = !string.IsNullOrEmpty(chapterTitle);
        _seekPreviewChapterTitle.Text = chapterTitle ?? string.Empty;

        // Keep invisible until SizeChanged positions it above the thumb.
        _seekPreview.IsVisible = true;
        PositionSeekPreview(time);
        if (_seekPreview.Width > 1 && _seekPreview.Height > 1)
            _seekPreview.Opacity = 1;

        var relativeUrl = _player.Source?.ThumbnailsUrl;
        var url = _server?.GetAbsoluteUri(relativeUrl)?.AbsoluteUri ?? relativeUrl;
        if (string.IsNullOrEmpty(url))
        {
            _seekPreviewImageBorder.IsVisible = false;
            return;
        }

        // Warm the sheet once; never cancel that download from scrub ticks.
        _ = EnsureSpriteBitmapAsync(url);

        var spriteIndex = NativeSeekThumbnailHelper.GetSpriteIndex(time);
        if (spriteIndex == _lastSpriteIndex && _seekPreviewImage.Source is not null)
        {
            _seekPreviewImageBorder.IsVisible = true;
            return;
        }

        ScheduleSpriteCrop(url, time, spriteIndex);
    }

    private void PositionSeekPreview(double time)
    {
        var duration = Math.Max(_player.Duration, 0.001);
        var ratio = Math.Clamp(time / duration, 0, 1);

        var barX = GetAbsoluteX(_seekBar);
        var barY = GetAbsoluteY(_seekBar);
        var barW = Math.Max(_seekBar.Width, 1);
        var thumbX = barX + barW * ratio;

        // Measure after layout; fall back to a stable width estimate.
        var previewW = _seekPreview.Width > 1 ? _seekPreview.Width : 180;
        var previewH = _seekPreview.Height > 1
            ? _seekPreview.Height
            : (NativeSeekThumbnailHelper.ThumbHeight / 2.0) + 56;
        var left = Math.Clamp(thumbX - previewW / 2, 8, Math.Max(8, Width - previewW - 8));
        var top = Math.Max(8, barY - previewH - 12);

        _seekPreview.TranslationX = left;
        _seekPreview.TranslationY = top;
        _seekPreview.Margin = Thickness.Zero;
    }

    private static double GetAbsoluteX(VisualElement element)
    {
        double x = element.X;
        for (var parent = element.Parent as VisualElement; parent is not null; parent = parent.Parent as VisualElement)
            x += parent.X;
        return x;
    }

    private static double GetAbsoluteY(VisualElement element)
    {
        double y = element.Y;
        for (var parent = element.Parent as VisualElement; parent is not null; parent = parent.Parent as VisualElement)
            y += parent.Y;
        return y;
    }

    private void ScheduleSpriteCrop(string url, double time, int spriteIndex)
    {
        _cropDebounceCts?.Cancel();
        _cropDebounceCts?.Dispose();
        _cropDebounceCts = new CancellationTokenSource();
        var token = _cropDebounceCts.Token;
        var version = Interlocked.Increment(ref _cropVersion);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(90, token);
                await EnsureSpriteBitmapAsync(url);
                if (token.IsCancellationRequested || version != _cropVersion || _spriteBitmap is null)
                    return;

                ApplySpriteCrop(time, spriteIndex, version);
            }
            catch (OperationCanceledException)
            {
                // Expected while scrubbing.
            }
            catch (Exception ex)
            {
                NativeVideoDebug.Log("SeekThumb fail: " + ex.GetType().Name);
            }
        }, token);
    }

    private void ApplySpriteCrop(double time, int spriteIndex, int version)
    {
        if (_spriteBitmap is null || version != _cropVersion)
            return;

        var (col, row) = NativeSeekThumbnailHelper.GetSpriteCell(time);
        var srcX = col * NativeSeekThumbnailHelper.ThumbWidth;
        var srcY = row * NativeSeekThumbnailHelper.ThumbHeight;
        if (srcX + NativeSeekThumbnailHelper.ThumbWidth > _spriteBitmap.Width
            || srcY + NativeSeekThumbnailHelper.ThumbHeight > _spriteBitmap.Height)
        {
            MainThread.BeginInvokeOnMainThread(() => _seekPreviewImageBorder.IsVisible = false);
            return;
        }

        using var subset = new SKBitmap(
            NativeSeekThumbnailHelper.ThumbWidth,
            NativeSeekThumbnailHelper.ThumbHeight);
        if (!_spriteBitmap.ExtractSubset(
                subset,
                new SKRectI(
                    srcX,
                    srcY,
                    srcX + NativeSeekThumbnailHelper.ThumbWidth,
                    srcY + NativeSeekThumbnailHelper.ThumbHeight)))
        {
            MainThread.BeginInvokeOnMainThread(() => _seekPreviewImageBorder.IsVisible = false);
            return;
        }

        using var image = SKImage.FromBitmap(subset);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 75);
        var bytes = data.ToArray();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (version != _cropVersion)
                return;
            _lastSpriteIndex = spriteIndex;
            _seekPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _seekPreviewImageBorder.IsVisible = true;
        });
    }

    private Task EnsureSpriteBitmapAsync(string url)
    {
        if (_spriteLoadedUrl == url && _spriteBitmap is not null)
            return Task.CompletedTask;

        if (_spriteLoadingUrl == url && _spriteLoadTask is not null)
            return _spriteLoadTask;

        _spriteLoadingUrl = url;
        _spriteLoadTask = LoadSpriteBitmapCoreAsync(url);
        return _spriteLoadTask;
    }

    private async Task LoadSpriteBitmapCoreAsync(string url)
    {
        HttpClient? client = null;
        var ownsClient = false;
        if (_server?.HttpClient is HttpClient shared)
            client = shared;
        else
        {
            client = new HttpClient();
            ownsClient = true;
        }

        try
        {
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var managed = new SKManagedStream(stream);
            var bitmap = SKBitmap.Decode(managed)
                ?? throw new InvalidOperationException("sprite decode failed");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisposeSpriteBitmap();
                _spriteBitmap = bitmap;
                _spriteLoadedUrl = url;
                _spriteLoadingUrl = null;
                NativeVideoDebug.Log(
                    "SeekThumb sheet loaded w=" + bitmap.Width + " h=" + bitmap.Height);
            });
        }
        catch (Exception ex)
        {
            _spriteLoadingUrl = null;
            NativeVideoDebug.Log("SeekThumb load fail: " + ex.GetType().Name);
            throw;
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
            if (_spriteLoadingUrl == url)
                _spriteLoadingUrl = null;
        }
    }

    private void DisposeSpriteBitmap()
    {
        _spriteBitmap?.Dispose();
        _spriteBitmap = null;
        _spriteLoadedUrl = null;
        _lastSpriteIndex = -1;
    }

    private void WarmSeekThumbnails()
    {
        var relativeUrl = _player.Source?.ThumbnailsUrl;
        var url = _server?.GetAbsoluteUri(relativeUrl)?.AbsoluteUri ?? relativeUrl;
        if (string.IsNullOrEmpty(url))
            return;
        _ = EnsureSpriteBitmapAsync(url);
    }

    private void DisposeSeekPreview()
    {
        _cropDebounceCts?.Cancel();
        _cropDebounceCts?.Dispose();
        _cropDebounceCts = null;
        DisposeSpriteBitmap();
        _spriteLoadTask = null;
        _spriteLoadingUrl = null;
        _seekPreviewImage.Source = null;
        _seekPreviewImageBorder.IsVisible = false;
        _seekPreview.IsVisible = false;
        _seekPreview.Opacity = 0;
    }

    private string? GetHoveredChapterTitle(double time)
    {
        var chapters = _seekBar.Chapters;
        string? title = null;
        for (var i = 0; i < chapters.Count; i++)
        {
            if (chapters[i].StartSeconds <= time)
                title = chapters[i].Title;
            else
                break;
        }

        return title;
    }
}
