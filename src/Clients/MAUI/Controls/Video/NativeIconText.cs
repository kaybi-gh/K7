namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Builds icon+text rows without forcing Latin text through the Phosphor font
/// (PUA-only glyph font has no ASCII letters).
/// </summary>
internal static class NativeIconText
{
    public static HorizontalStackLayout CreateContent(
        string? glyph,
        string text,
        double fontSize = 15,
        View? leading = null)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center
        };

        if (leading is not null)
            row.Children.Add(leading);

        if (!string.IsNullOrEmpty(glyph))
        {
            row.Children.Add(new Label
            {
                Text = glyph,
                FontFamily = NativePlayerGlyphs.FontFamily,
                TextColor = Colors.White,
                FontSize = fontSize,
                VerticalOptions = LayoutOptions.Center,
                FontAutoScalingEnabled = false
            });
        }

        row.Children.Add(new Label
        {
            Text = text,
            // App.xaml defaults Labels to OpenSansRegular; set explicitly so siblings with
            // Phosphor cannot leave Latin text unmapped / zero-width on Windows.
            FontFamily = "OpenSansRegular",
            TextColor = Colors.White,
            FontSize = fontSize,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            HorizontalOptions = LayoutOptions.Fill,
            FontAutoScalingEnabled = false
        });

        return row;
    }

    public static Border CreateTappableRow(
        string? glyph,
        string text,
        bool selected,
        Action onClick,
        double fontSize = 15)
    {
        var border = new Border
        {
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            BackgroundColor = selected ? Color.FromArgb("#33FFFFFF") : Colors.Transparent,
            Padding = new Thickness(12, 10),
            Content = CreateContent(glyph, text, fontSize),
            HorizontalOptions = LayoutOptions.Fill
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onClick();
        border.GestureRecognizers.Add(tap);
        return border;
    }

    public static void SetHud(Label iconLabel, Label textLabel, string icon, string text)
    {
        iconLabel.Text = icon;
        iconLabel.FontFamily = NativePlayerGlyphs.FontFamily;
        textLabel.Text = text;
        textLabel.FontFamily = null;
    }
}
