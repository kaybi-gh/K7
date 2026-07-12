using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaReviewCard
{
    [Inject] private IStringLocalizer<MediaReviewCard> L { get; set; } = default!;

    [Parameter] public string? DisplayName { get; set; }
    [Parameter] public string? ProfileHref { get; set; }
    [Parameter] public Guid? UserId { get; set; }
    [Parameter] public string? AvatarUrl { get; set; }
    [Parameter] public bool IsFederated { get; set; }
    [Parameter] public string? PeerName { get; set; }
    [Parameter] public double Rating { get; set; }
    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Emoji { get; set; }
    [Parameter] public DateTimeOffset? Created { get; set; }
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool Flat { get; set; }
    [Parameter] public bool HideAuthor { get; set; }
    [Parameter] public RenderFragment? LeadingContent { get; set; }
    [Parameter] public string? LeadingLabel { get; set; }
    [Parameter] public MediaCardVariant? LeadingArtworkVariant { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool IsSpoilerBlurred { get; set; }
    [Parameter] public EventCallback OnActivate { get; set; }
    [Parameter] public string? ActivateAriaLabel { get; set; }

    private bool _spoilerRevealed;

    private bool ShowAuthor => !HideAuthor;

    private bool IsActivatable => OnActivate.HasDelegate;

    private bool ShowSpoilerOverlay => IsSpoilerBlurred && !_spoilerRevealed;

    private bool ShowLeadingSlot => LeadingContent is not null;

    private bool ShowLeadingLabel => !string.IsNullOrWhiteSpace(LeadingLabel);

    private bool HasRating => Rating > 0;

    private bool HasText => !string.IsNullOrWhiteSpace(Text);

    private bool ShowFooter => Created is not null || Actions is not null || ChildContent is not null;

    private string RootClass =>
        string.Join(' ',
            new[]
            {
                Compact ? "media-review-card--compact" : null,
                Flat ? "media-review-card--flat" : null,
                HideAuthor ? "media-review-card--hide-author" : null,
                ShowLeadingSlot ? "media-review-card--has-leading" : null,
                LeadingArtworkVariant is MediaCardVariant.Backdrop ? "media-review-card--leading-backdrop" : null,
                LeadingArtworkVariant is MediaCardVariant.Cover ? "media-review-card--leading-cover" : null,
                ShowLeadingSlot && LeadingArtworkVariant is null or MediaCardVariant.Poster ? "media-review-card--leading-poster" : null,
                ShowLeadingSlot ? "media-review-card--leading-focusable" : null,
                IsActivatable ? "media-review-card--activatable" : null,
                IsActivatable && ShowLeadingSlot ? "media-review-card--activatable-leading" : null,
                IsActivatable && !ShowLeadingSlot ? "focusable" : null,
                ShowSpoilerOverlay ? "media-review-card--spoiler-blurred" : null
            }.OfType<string>());

    private string ResolvedDisplayName =>
        string.IsNullOrWhiteSpace(DisplayName) ? L["Anonymous"] : DisplayName;

    private int? RatingValue => HasRating ? (int)Math.Round(Rating) : null;

    private string OriginLabel =>
        IsFederated && !string.IsNullOrWhiteSpace(PeerName)
            ? string.Format(L["FederatedUserOnPeer"], PeerName)
            : L["LocalUser"];

    private string RatingLabel => string.Format(L["RatingOutOfTen"], Rating.ToString("N0"));

    private string AvatarInitial =>
        ResolvedDisplayName.Length > 0
            ? char.ToUpperInvariant(ResolvedDisplayName[0]).ToString()
            : "";

    protected override void OnParametersSet()
    {
        if (!IsSpoilerBlurred)
            _spoilerRevealed = false;
    }

    private void RevealSpoiler()
    {
        _spoilerRevealed = true;
    }

    private async Task HandleActivateAsync()
    {
        if (!IsActivatable || ShowSpoilerOverlay)
            return;

        await OnActivate.InvokeAsync();
    }

    private Task HandleArticleClickAsync() =>
        ShowLeadingSlot ? Task.CompletedTask : HandleActivateAsync();

    private Task HandleBodyClickAsync() =>
        IsActivatable && ShowLeadingSlot ? HandleActivateAsync() : Task.CompletedTask;

    private async Task HandleArticleKeyDownAsync(KeyboardEventArgs e)
    {
        if (ShowLeadingSlot)
            return;

        await HandleKeyDownAsync(e);
    }

    private async Task HandleBodyKeyDownAsync(KeyboardEventArgs e)
    {
        if (!IsActivatable || !ShowLeadingSlot)
            return;

        await HandleKeyDownAsync(e);
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (!IsActivatable || ShowSpoilerOverlay)
            return;

        if (e.Key is "Enter" or " ")
            await OnActivate.InvokeAsync();
    }

    private string FormatRelativeTime(DateTimeOffset created)
    {
        var diff = DateTimeOffset.UtcNow - created.ToUniversalTime();
        if (diff.TotalMinutes < 1)
            return L["JustNow"];
        if (diff.TotalMinutes < 60)
            return string.Format(L["MinutesAgo"], (int)diff.TotalMinutes);
        if (diff.TotalHours < 24)
            return string.Format(L["HoursAgo"], (int)diff.TotalHours);
        return string.Format(L["DaysAgo"], (int)diff.TotalDays);
    }
}
