using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Server.Application.Features.CustomNav;

public class CustomNavLayoutValidator : AbstractValidator<CustomNavLayoutDto>
{
    public CustomNavLayoutValidator()
    {
        RuleFor(v => v.Placement).IsInEnum();
        RuleFor(v => v.BarScope).IsInEnum().When(v => v.BarScope is not null);
        RuleFor(v => v.FeedRowScope).IsInEnum().When(v => v.FeedRowScope is not null);
        RuleFor(v => v.FeedRowTitle).MaximumLength(CustomNavLimits.MaxTitleLength);
        RuleFor(v => v.Items)
            .NotNull()
            .Must(items => items.Count <= CustomNavLimits.MaxItems)
            .WithMessage($"At most {CustomNavLimits.MaxItems} custom navigation items are allowed.");

        RuleForEach(v => v.Items).SetValidator(new CustomNavItemValidator());
    }
}

public class CustomNavItemValidator : AbstractValidator<CustomNavItemDto>
{
    public CustomNavItemValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Kind).IsInEnum();
        RuleFor(v => v.Title).MaximumLength(CustomNavLimits.MaxTitleLength);
        RuleFor(v => v.BrowseQuery).MaximumLength(CustomNavLimits.MaxBrowseQueryLength);
        RuleFor(v => v.Route).MaximumLength(CustomNavLimits.MaxRouteLength);
        RuleFor(v => v.Icon).MaximumLength(64);
        RuleFor(v => v.CardColor)
            .MaximumLength(CustomNavLimits.MaxCardColorLength)
            .Matches("^#[0-9A-Fa-f]{6}$")
            .When(v => !string.IsNullOrWhiteSpace(v.CardColor));
        RuleFor(v => v.CoverPictureId)
            .NotEmpty()
            .When(v => v.CoverPictureId is not null);
        RuleFor(v => v.TapAction).IsInEnum().When(v => v.TapAction is not null);

        RuleFor(v => v.LibraryGroupId)
            .NotEmpty()
            .When(v => v.Kind is CustomNavItemKind.LibraryGroup or CustomNavItemKind.LibraryBrowse);

        RuleFor(v => v.TargetId)
            .NotEmpty()
            .When(v => v.Kind is CustomNavItemKind.Collection or CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist);

        RuleFor(v => v.Route)
            .NotEmpty()
            .Must(route => CustomNavRoutes.TryNormalize(route, out _))
            .When(v => v.Kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute)
            .WithMessage("Route is not in the custom navigation allowlist.");
    }
}
