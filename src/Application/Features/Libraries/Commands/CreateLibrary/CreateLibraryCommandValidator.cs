using MediaServer.Application.Common.Interfaces;

namespace MediaServer.Application.Features.Libraries.Commands.CreateLibrary;

public class CreateLibraryCommandValidator : AbstractValidator<CreateLibraryCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateLibraryCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueTitle)
                .WithMessage("'{PropertyName}' must be unique.")
                .WithErrorCode("Unique");

        RuleFor(v => v.MediaType)
            .IsInEnum()
            .NotEmpty();

        RuleFor(v => v.RootPath)
            .NotEmpty();
    }

    public async Task<bool> BeUniqueTitle(string title, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .AllAsync(l => l.Title != title, cancellationToken);
    }
}
