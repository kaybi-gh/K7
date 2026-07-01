using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7DialogStepper
{
    [Parameter, EditorRequired] public IReadOnlyList<string> Steps { get; set; } = [];
    [Parameter] public int ActiveStep { get; set; }
    [Parameter] public int MaxVisitedStep { get; set; }
    [Parameter] public EventCallback<int> OnStepClick { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private bool IsStepReachable(int step) => step <= MaxVisitedStep;

    private string GetStepStyle(int step) =>
        IsStepReachable(step) ? "cursor: pointer;" : "opacity: 0.5; pointer-events: none;";

    private async Task OnStepSelected(int step)
    {
        if (!IsStepReachable(step))
            return;

        await OnStepClick.InvokeAsync(step);
    }
}
