namespace K7.Server.Application.Features.Federation.Commands.UpdatePeer;

public class UpdatePeerCommandValidator : AbstractValidator<UpdatePeerCommand>
{
    public UpdatePeerCommandValidator()
    {
        RuleFor(v => v.PeerId)
            .NotEmpty();

        RuleFor(v => v.BaseUrl)
            .MaximumLength(2000)
            .Must(BeAWellFormedHttpUrl)
            .WithMessage("BaseUrl must be an absolute HTTP or HTTPS URL.")
            .When(v => !string.IsNullOrWhiteSpace(v.BaseUrl));

        RuleFor(v => v.MaxConcurrentStreams)
            .GreaterThan(0)
            .When(v => v.MaxConcurrentStreams is not null);

        RuleForEach(v => v.SharedLibraryIds)
            .NotEmpty()
            .When(v => v.SharedLibraryIds is not null);

        RuleForEach(v => v.EnabledInboundAgreementIds)
            .NotEmpty()
            .When(v => v.EnabledInboundAgreementIds is not null);

        RuleForEach(v => v.SharePlaybackHistoryLibraryIds)
            .NotEmpty()
            .When(v => v.SharePlaybackHistoryLibraryIds is not null);

        RuleForEach(v => v.SocialAgreements).ChildRules(item =>
        {
            item.RuleFor(i => i.Id).NotEmpty();
            item.RuleFor(i => i.ContentType).IsInEnum();
        }).When(v => v.SocialAgreements is not null);
    }

    private static bool BeAWellFormedHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
