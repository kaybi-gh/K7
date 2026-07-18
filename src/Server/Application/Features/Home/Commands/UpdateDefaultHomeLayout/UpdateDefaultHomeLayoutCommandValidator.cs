using K7.Server.Application.Common.Models;

namespace K7.Server.Application.Features.Home.Commands.UpdateDefaultHomeLayout;

public class UpdateDefaultHomeLayoutCommandValidator : AbstractValidator<UpdateDefaultHomeLayoutCommand>
{
    public UpdateDefaultHomeLayoutCommandValidator()
    {
        RuleFor(v => v.Layout)
            .NotNull();

        RuleFor(v => v.Layout.Rows)
            .NotNull()
            .When(v => v.Layout is not null);

        RuleForEach(v => v.Layout.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.Id).NotEmpty();
            row.RuleFor(r => r.Title).NotEmpty().MaximumLength(200);
            row.RuleFor(r => r.DisplayType).IsInEnum();
            row.RuleFor(r => r.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize);
            row.RuleFor(r => r.Order).GreaterThanOrEqualTo(0);
            row.RuleForEach(r => r.MediaTypes).IsInEnum().When(r => r.MediaTypes is not null);
            row.RuleForEach(r => r.OrderBy).IsInEnum().When(r => r.OrderBy is not null);
        }).When(v => v.Layout?.Rows is not null);
    }
}
