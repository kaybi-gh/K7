using K7.Shared.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Server.Web.Components.Account.Shared;

public partial class AuthEmailField
{
    [Inject] private IStringLocalizer<AuthEmailField> L { get; set; } = default!;

    [Parameter] public string Name { get; set; } = "Input.Email";
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public string Autocomplete { get; set; } = "email";

    private bool ShowInvalid =>
        !string.IsNullOrWhiteSpace(Value) && !EmailFormat.IsValidRequired(Value);

    private string InvalidId => $"{Name.Replace('.', '-')}-invalid";
}
