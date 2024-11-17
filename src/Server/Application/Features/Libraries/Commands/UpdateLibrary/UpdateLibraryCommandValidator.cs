using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;

public class UpdateLibraryCommandValidator : AbstractValidator<UpdateLibraryCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateLibraryCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueTitle)
                .WithMessage("'{PropertyName}' must be unique.")
                .WithErrorCode("Unique");
    }

    public async Task<bool> BeUniqueTitle(UpdateLibraryCommand model, string title, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .Where(l => l.Id != model.Id)
            .AllAsync(l => l.Title != title, cancellationToken);
    }
}
