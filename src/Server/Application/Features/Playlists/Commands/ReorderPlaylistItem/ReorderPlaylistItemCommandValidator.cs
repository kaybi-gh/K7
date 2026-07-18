namespace K7.Server.Application.Features.Playlists.Commands.ReorderPlaylistItem;

public class ReorderPlaylistItemCommandValidator : AbstractValidator<ReorderPlaylistItemCommand>
{
    public ReorderPlaylistItemCommandValidator()
    {
        RuleFor(v => v.PlaylistId)
            .NotEmpty();

        RuleFor(v => v.ItemId)
            .NotEmpty();

        RuleFor(v => v.NewOrder)
            .GreaterThanOrEqualTo(0);
    }
}
