namespace K7.Server.Application.Features.Federation.Commands.InitiatePeering;

public class InitiatePeeringCommandValidator : AbstractValidator<InitiatePeeringCommand>
{
    public InitiatePeeringCommandValidator()
    {
        RuleFor(v => v.RemoteUrl)
            .NotEmpty()
            .MaximumLength(2000)
            .Must(BeAWellFormedHttpUrl)
            .WithMessage("RemoteUrl must be an absolute HTTP or HTTPS URL.");

        RuleFor(v => v.LocalServerName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.LocalServerUrl)
            .NotEmpty()
            .MaximumLength(2000)
            .Must(BeAWellFormedHttpUrl)
            .WithMessage("LocalServerUrl must be an absolute HTTP or HTTPS URL.");
    }

    private static bool BeAWellFormedHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
