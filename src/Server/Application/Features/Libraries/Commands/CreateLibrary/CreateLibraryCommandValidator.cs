using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Libraries.Commands.CreateLibrary;

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
            .NotEmpty()
            .MustAsync(BeUniqueRootPath)
                .WithMessage("'{PropertyName}' must be unique.")
                .WithErrorCode("Unique");
    }

    public async Task<bool> BeUniqueTitle(string title, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .AllAsync(l => l.Title != title, cancellationToken);
    }

    public async Task<bool> BeUniqueRootPath(string rootPath, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .AllAsync(l => l.RootPath != rootPath, cancellationToken);
    }
}
