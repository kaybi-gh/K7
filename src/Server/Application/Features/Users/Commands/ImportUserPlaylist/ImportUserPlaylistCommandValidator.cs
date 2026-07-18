namespace K7.Server.Application.Features.Users.Commands.ImportUserPlaylist;

public class ImportUserPlaylistCommandValidator : AbstractValidator<ImportUserPlaylistCommand>
{
    public ImportUserPlaylistCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
    }
}
