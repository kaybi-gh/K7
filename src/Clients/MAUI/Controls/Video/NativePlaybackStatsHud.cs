using K7.Clients.Shared.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Admin-only live playback HUD. Lives on RootGrid (sibling of the chrome overlay)
/// so TV can keep the overlay undrawn while stats stay visible.
/// </summary>
internal sealed class NativePlaybackStatsHud : Border
{
    private readonly Label _headline = CreateLabel(15, FontAttributes.Bold, Colors.White);
    private readonly Label _decision = CreateLabel(13, FontAttributes.None, Color.FromArgb("#E6FFFFFF"));
    private readonly Label _runtime = CreateLabel(12, FontAttributes.None, Color.FromArgb("#CCFFFFFF"));
    private readonly Label _policy = CreateLabel(11, FontAttributes.None, Color.FromArgb("#99FFFFFF"));

    public NativePlaybackStatsHud()
    {
        IsVisible = false;
        InputTransparent = true;
        BackgroundColor = Color.FromArgb("#E6121212");
        Stroke = Color.FromArgb("#33FFFFFF");
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        Padding = new Thickness(14, 12);
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.Start;
        Margin = new Thickness(16, 20, 16, 16);
        MaximumWidthRequest = 460;
        ZIndex = 6;

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _headline,
                _decision,
                _runtime,
                _policy
            }
        };
    }

    public void SetSnapshot(NativePlaybackStatsSnapshot snapshot)
    {
        var headline = NativePlaybackStatsFormatting.HeaderLine(snapshot);
        _headline.Text = headline;
        _headline.IsVisible = !string.IsNullOrWhiteSpace(headline);

        var decision = NativePlaybackStatsFormatting.DecisionBlock(snapshot);
        _decision.Text = decision;
        _decision.IsVisible = !string.IsNullOrWhiteSpace(decision);

        var runtime = NativePlaybackStatsFormatting.RuntimeBlock(snapshot);
        _runtime.Text = runtime;
        _runtime.TextColor = snapshot.CadenceWarning
            ? Color.FromArgb("#FFCC66")
            : Color.FromArgb("#CCFFFFFF");
        _runtime.IsVisible = !string.IsNullOrWhiteSpace(runtime);

        var policy = NativePlaybackStatsFormatting.PolicyBlock(snapshot);
        _policy.Text = policy;
        _policy.IsVisible = !string.IsNullOrWhiteSpace(policy);
    }

    private static Label CreateLabel(double fontSize, FontAttributes attributes, Color color) =>
        new()
        {
            TextColor = color,
            FontSize = fontSize,
            FontAttributes = attributes,
            FontFamily = "OpenSansRegular",
            LineBreakMode = LineBreakMode.WordWrap,
            FontAutoScalingEnabled = false
        };
}
