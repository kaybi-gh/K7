using MudBlazor;

namespace MediaClient.Shared.Components;

public partial class CustomMudProviders
{
    protected override void OnInitialized()
    {
        Snackbar.Clear();
        Snackbar.Configuration.PositionClass = Defaults.Classes.Position.TopCenter;
        Snackbar.Configuration.MaxDisplayedSnackbars = 5;
        Snackbar.Configuration.SnackbarVariant = Variant.Filled;
        Snackbar.Configuration.PreventDuplicates = true;
    }
}