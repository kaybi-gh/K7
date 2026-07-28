namespace K7.Server.Application.Features.Medias.Commands.BulkCreateMedias;

public class BulkCreateMediasCommandValidator : AbstractValidator<BulkCreateMediasCommand>
{
    public BulkCreateMediasCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Key).NotEmpty();
            item.RuleFor(x => x.MediaType).NotEmpty()
                .Must(t => t is "movie" or "music" or "episode" or "serie")
                .WithMessage("MediaType must be 'movie', 'music', 'episode', or 'serie'");
            item.RuleFor(x => x.Title).NotEmpty();
        });
    }
}
