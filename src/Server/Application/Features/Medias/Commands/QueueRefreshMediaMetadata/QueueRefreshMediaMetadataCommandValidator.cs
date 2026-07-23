namespace K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;

public class QueueRefreshMediaMetadataCommandValidator : AbstractValidator<QueueRefreshMediaMetadataCommand>
{
    public QueueRefreshMediaMetadataCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
