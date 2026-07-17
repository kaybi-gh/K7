namespace K7.Server.Application.Features.Medias.Commands.BulkLinkArtists;

public class BulkLinkArtistsCommandValidator : AbstractValidator<BulkLinkArtistsCommand>
{
    public BulkLinkArtistsCommandValidator()
    {
        RuleFor(x => x.Items).NotNull();
    }
}
