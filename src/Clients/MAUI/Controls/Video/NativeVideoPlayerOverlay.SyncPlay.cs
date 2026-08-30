using K7.Shared.Dtos;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// SyncPlay sidebar-equivalent panel: members with ready state, leave, chat, reaction picker,
/// plus an always-on floating reaction overlay. Mirrors <c>SyncPlayOverlay.razor(.cs)</c>,
/// <c>SyncPlayChat.razor(.cs)</c>, <c>SyncPlayReactionPicker.razor(.cs)</c>, and
/// <c>SyncPlayReactionOverlay.razor(.cs)</c>.
/// </summary>
public sealed partial class NativeVideoPlayerOverlay
{
    private static readonly Random _reactionRandom = new();

    private readonly Border _syncPlayPanel = new();
    private readonly Label _syncPlayHeaderLabel = new();
    private readonly Label _syncPlayWaitingLabel = new();
    private readonly VerticalStackLayout _syncPlayParticipantsList = new() { Spacing = 4 };
    private readonly VerticalStackLayout _syncPlayChatList = new() { Spacing = 6, Padding = new Thickness(8) };
    private readonly Entry _syncPlayChatInput = new();
    private readonly FlexLayout _reactionPickerGrid = new() { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
    private readonly Border _reactionPickerPopover = new();
    private readonly AbsoluteLayout _reactionLayer = new() { InputTransparent = true };

    private bool _showParticipants;
    private bool _showReactionPicker;

    private void SubscribeSyncPlay()
    {
        if (_syncPlay is null)
            return;

        _syncPlay.GroupUpdated += OnSyncPlayGroupUpdated;
        _syncPlay.ChatMessageReceived += OnSyncPlayChatReceived;
        _syncPlay.ReactionReceived += OnSyncPlayReactionReceived;
    }

    private void UnsubscribeSyncPlay()
    {
        if (_syncPlay is null)
            return;

        _syncPlay.GroupUpdated -= OnSyncPlayGroupUpdated;
        _syncPlay.ChatMessageReceived -= OnSyncPlayChatReceived;
        _syncPlay.ReactionReceived -= OnSyncPlayReactionReceived;
    }

    private void OnSyncPlayGroupUpdated() => MainThread.BeginInvokeOnMainThread(() =>
    {
        _syncPlayButton.IsVisible = _syncPlay?.IsInGroup == true;
        if (_syncPlayPanelOpen)
            RebuildSyncPlayPanel();
    });

    private void OnSyncPlayChatReceived(SyncPlayChatMessageDto _) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (_syncPlayPanelOpen)
            RebuildChatList();
    });

    private void OnSyncPlayReactionReceived(SyncPlayReactionDto reaction) =>
        MainThread.BeginInvokeOnMainThread(() => _ = ShowFloatingReactionAsync(reaction));

    private void BuildSyncPlayPanel()
    {
        var root = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };

        var badge = new HorizontalStackLayout { Spacing = 6 };
        badge.Children.Add(new Label
        {
            Text = NativePlayerGlyphs.SyncPlay,
            FontFamily = NativePlayerGlyphs.FontFamily,
            TextColor = Colors.White,
            FontSize = 14
        });
        _syncPlayHeaderLabel.TextColor = Colors.White;
        _syncPlayHeaderLabel.FontSize = 15;
        _syncPlayHeaderLabel.FontAttributes = FontAttributes.Bold;
        badge.Children.Add(_syncPlayHeaderLabel);
        root.Children.Add(badge);

        _syncPlayWaitingLabel.TextColor = Color.FromArgb("#CCFFFFFF");
        _syncPlayWaitingLabel.FontSize = 12;
        _syncPlayWaitingLabel.IsVisible = false;
        root.Children.Add(_syncPlayWaitingLabel);

        // Chat.
        var chatScroll = new ScrollView { Content = _syncPlayChatList, HeightRequest = 140 };
        root.Children.Add(chatScroll);

        _syncPlayChatInput.Placeholder = NativeStrings.MessagePlaceholder;
        _syncPlayChatInput.TextColor = Colors.White;
        _syncPlayChatInput.PlaceholderColor = Color.FromArgb("#88FFFFFF");
        _syncPlayChatInput.ReturnType = ReturnType.Send;
        _syncPlayChatInput.Completed += (_, _) => _ = SendSyncPlayChatAsync();

        var sendButton = CreateSyncPlayIconButton(NativePlayerGlyphs.PaperPlaneTilt);
        sendButton.Clicked += (_, _) => _ = SendSyncPlayChatAsync();

        var chatInputRow = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 4 };
        chatInputRow.Children.Add(_syncPlayChatInput);
        Grid.SetColumn(sendButton, 1);
        chatInputRow.Children.Add(sendButton);
        root.Children.Add(chatInputRow);

        // Reaction picker popover (toggled by the smiley button below).
        _reactionPickerPopover.BackgroundColor = Color.FromArgb("#EE121212");
        _reactionPickerPopover.StrokeShape = new RoundRectangle { CornerRadius = 10 };
        _reactionPickerPopover.Padding = new Thickness(6);
        _reactionPickerPopover.IsVisible = false;
        _reactionPickerPopover.Content = _reactionPickerGrid;
        foreach (var emoji in K7.Clients.Shared.UI.Components.K7EmojiPalette.All)
        {
            var emojiButton = new Button
            {
                Text = emoji,
                FontSize = 20,
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(6),
                CornerRadius = 8
            };
            NativeOverlayHover.Attach(emojiButton, hovered =>
                emojiButton.BackgroundColor = hovered ? NativeOverlayHover.Highlight : Colors.Transparent);
            emojiButton.Clicked += (_, _) => _ = SendSyncPlayReactionAsync(emoji);
            _reactionPickerGrid.Children.Add(emojiButton);
        }
        root.Children.Add(_reactionPickerPopover);

        // Actions row: participants toggle, reaction picker toggle, leave.
        var actions = new HorizontalStackLayout { Spacing = 8 };
        var usersToggle = CreateSyncPlayIconButton($"{NativePlayerGlyphs.Users}");
        usersToggle.Clicked += (_, _) =>
        {
            _showParticipants = !_showParticipants;
            _syncPlayParticipantsList.IsVisible = _showParticipants;
        };
        actions.Children.Add(usersToggle);

        var reactionToggle = CreateSyncPlayIconButton(NativePlayerGlyphs.Bell);
        reactionToggle.Clicked += (_, _) =>
        {
            _showReactionPicker = !_showReactionPicker;
            _reactionPickerPopover.IsVisible = _showReactionPicker;
        };
        actions.Children.Add(reactionToggle);

        var leaveButton = CreateSyncPlayIconButton(NativePlayerGlyphs.SignOut);
        leaveButton.Clicked += (_, _) => _ = LeaveSyncPlayGroupAsync();
        actions.Children.Add(leaveButton);
        root.Children.Add(actions);

        _syncPlayParticipantsList.IsVisible = false;
        root.Children.Add(_syncPlayParticipantsList);

        _syncPlayPanel.Content = new ScrollView { Content = root };
        _syncPlayPanel.BackgroundColor = Color.FromArgb("#E6121212");
        _syncPlayPanel.Stroke = Colors.Transparent;
        _syncPlayPanel.StrokeShape = new RoundRectangle { CornerRadius = 12 };
        _syncPlayPanel.Padding = new Thickness(4);
        _syncPlayPanel.WidthRequest = 300;
        _syncPlayPanel.MaximumHeightRequest = 440;
        _syncPlayPanel.IsVisible = false;
        _syncPlayPanel.HorizontalOptions = LayoutOptions.End;
        _syncPlayPanel.VerticalOptions = LayoutOptions.End;
        _syncPlayPanel.Margin = new Thickness(16, 16, 16, 88);
        Children.Add(_syncPlayPanel);
    }

    private void BuildReactionLayer()
    {
        _reactionLayer.HorizontalOptions = LayoutOptions.Fill;
        _reactionLayer.VerticalOptions = LayoutOptions.Fill;
        Children.Add(_reactionLayer);
    }

    private static Button CreateSyncPlayIconButton(string glyph)
    {
        var button = new Button
        {
            Text = glyph,
            FontFamily = NativePlayerGlyphs.FontFamily,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
            FontSize = 18,
            Padding = new Thickness(8, 4),
            CornerRadius = 8
        };
        NativeOverlayHover.Attach(button, hovered =>
            button.BackgroundColor = hovered ? NativeOverlayHover.Highlight : Colors.Transparent);
        return button;
    }

    private void ToggleSyncPlayPanel() => SetSyncPlayPanelOpen(!_syncPlayPanelOpen);

    private void SetSyncPlayPanelOpen(bool open)
    {
        _syncPlayPanelOpen = open;
        _syncPlayPanel.IsVisible = open && _syncPlay?.IsInGroup == true;
        if (open)
        {
            _settings.Close();
            SetCastPanelOpen(false);
            StopHideTimer();
            RebuildSyncPlayPanel();
        }
        else
        {
            _showReactionPicker = false;
            _reactionPickerPopover.IsVisible = false;
            ResetHideTimer();
        }

        UpdateChromeVisibility();
    }

    private void RebuildSyncPlayPanel()
    {
        var group = _syncPlay?.CurrentGroup;
        if (group is null)
            return;

        _syncPlayHeaderLabel.Text = $"{group.GroupName} ({group.Participants.Count})";
        _syncPlayWaitingLabel.IsVisible = group.State == SyncPlayGroupState.WaitingForReady;
        _syncPlayWaitingLabel.Text = NativeStrings.WaitingForOthers;

        RebuildChatList();

        _syncPlayParticipantsList.Children.Clear();
        foreach (var participant in group.Participants)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };
            row.Children.Add(new Label
            {
                Text = participant.DisplayName.Length > 0 ? participant.DisplayName[..1].ToUpperInvariant() : "?",
                TextColor = Colors.White,
                BackgroundColor = Color.FromArgb("#33FFFFFF"),
                WidthRequest = 24,
                HeightRequest = 24,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 12
            });
            row.Children.Add(new Label
            {
                Text = participant.DisplayName,
                TextColor = participant.IsReady ? Colors.White : Color.FromArgb("#99FFFFFF"),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center
            });
            _syncPlayParticipantsList.Children.Add(row);
        }
    }

    private void RebuildChatList()
    {
        if (_syncPlay is null)
            return;

        _syncPlayChatList.Children.Clear();
        foreach (var msg in _syncPlay.ChatMessages.TakeLast(50))
        {
            if (string.IsNullOrEmpty(msg.DisplayName))
            {
                _syncPlayChatList.Children.Add(new Label
                {
                    Text = msg.Text,
                    TextColor = Color.FromArgb("#99FFFFFF"),
                    FontSize = 12,
                    HorizontalTextAlignment = TextAlignment.Center
                });
                continue;
            }

            var row = new VerticalStackLayout { Spacing = 1 };
            row.Children.Add(new Label { Text = msg.DisplayName, TextColor = Color.FromArgb("#CCFFFFFF"), FontSize = 11, FontAttributes = FontAttributes.Bold });
            row.Children.Add(new Label { Text = msg.Text, TextColor = Colors.White, FontSize = 13 });
            _syncPlayChatList.Children.Add(row);
        }
    }

    private async Task SendSyncPlayChatAsync()
    {
        if (_syncPlay is null || string.IsNullOrWhiteSpace(_syncPlayChatInput.Text))
            return;

        var text = _syncPlayChatInput.Text.Trim();
        _syncPlayChatInput.Text = string.Empty;
        await _syncPlay.SendChatAsync(text);
    }

    private async Task SendSyncPlayReactionAsync(string emoji)
    {
        _showReactionPicker = false;
        _reactionPickerPopover.IsVisible = false;
        if (_syncPlay is not null)
            await _syncPlay.SendReactionAsync(emoji);
    }

    private async Task LeaveSyncPlayGroupAsync()
    {
        if (_syncPlay is not null)
            await _syncPlay.LeaveGroupAsync();
        SetSyncPlayPanelOpen(false);
        UpdateTransport();
    }

    /// <summary>Floating emoji bubble that fades out after ~3s - shown regardless of whether
    /// the SyncPlay panel is open, matching the independent SyncPlayReactionOverlay component.</summary>
    private async Task ShowFloatingReactionAsync(SyncPlayReactionDto reaction)
    {
        if (_syncPlay?.ShowReactions != true)
            return;

        var bubble = new Label
        {
            Text = reaction.Emoji,
            FontSize = 32,
            Opacity = 0
        };

        var xPercent = _reactionRandom.Next(10, 80) / 100.0;
        AbsoluteLayout.SetLayoutFlags(bubble, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(bubble, new Rect(xPercent, 0.75, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        _reactionLayer.Children.Add(bubble);

        await bubble.FadeToAsync(1, 150);
        await Task.Delay(2500);
        await bubble.FadeToAsync(0, 500);
        _reactionLayer.Children.Remove(bubble);
    }
}
