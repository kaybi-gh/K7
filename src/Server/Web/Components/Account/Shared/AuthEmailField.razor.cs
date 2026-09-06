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

    private string _email = "";

    private bool ShowInvalid =>
        !string.IsNullOrWhiteSpace(_email) && !EmailFormat.IsValidRequired(_email);

    private string InvalidId => $"{Name.Replace('.', '-')}-invalid";

    protected override void OnParametersSet()
    {
        if (string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(Value))
            _email = Value;
    }

    private void OnInput(ChangeEventArgs args) =>
        _email = args.Value?.ToString() ?? "";
}
