using System.ComponentModel.DataAnnotations;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Components.Account;
using Microsoft.AspNetCore.Components;

namespace K7.Server.Web.Components.Pages.Setup;

public partial class SetupWizard
{
    private string? _statusMessage;

    [SupplyParameterFromForm]
    private InputModel? Input { get; set; }

    protected override void OnInitialized()
    {
        Input ??= new();
    }

    private async Task OnSubmitAsync()
    {
        if (await SetupService.IsSetupCompletedAsync())
        {
            RedirectManager.RedirectTo("/");
            return;
        }

        var result = await SetupService.CompleteSetupAsync(Input!.Email, Input.Password);

        if (result.Succeeded)
        {
            var user = await UserManager.FindByEmailAsync(Input.Email);
            await SignInManager.SignInAsync(user!, isPersistent: false);
            RedirectManager.RedirectTo("/");
            return;
        }

        _statusMessage = string.Join(" ", result.Errors);
    }

    private sealed class InputModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required.")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
