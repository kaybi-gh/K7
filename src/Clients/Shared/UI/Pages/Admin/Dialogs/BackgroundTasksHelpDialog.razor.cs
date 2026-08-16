using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Pages.Admin.Panels;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class BackgroundTasksHelpDialog
{
    private static readonly string[] CatalogTaskNames =
    [
        "IndexLibraryFilesCommand",
        "CreateMediaCommand",
        "RematchLibraryMediaCommand",
        "RefreshMediaMetadatasCommand",
        "CreateFileMetadatasCommand",
        "ExtractChaptersCommand",
        "ComputeHlsSegmentsCommand",
        "DetectMediaSegmentsCommand",
        "ExtractSerieThemeSongCommand",
        "AnalyzeMusicTrackAudioCommand"
    ];

    private static readonly BackgroundTaskWorkClass[] WorkClasses =
    [
        BackgroundTaskWorkClass.CriticalProbe,
        BackgroundTaskWorkClass.CriticalLink,
        BackgroundTaskWorkClass.CriticalEnrich,
        BackgroundTaskWorkClass.Prepare,
        BackgroundTaskWorkClass.Polish
    ];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Inject] private IStringLocalizer<AdminBackgroundTasksPanel> TasksL { get; set; } = default!;

    private IReadOnlyList<string> TaskCatalog => CatalogTaskNames;

    private IReadOnlyList<BackgroundTaskWorkClass> WorkClassCatalog => WorkClasses;

    private void Close() => Dialog.Cancel();

    private string GetTaskTypeLabel(string taskName) =>
        BackgroundTaskLabelHelper.GetTaskTypeLabel(TasksL, taskName);

    private string GetWorkClassLabel(BackgroundTaskWorkClass workClass) =>
        BackgroundTaskLabelHelper.GetWorkClassLabel(TasksL, workClass);

    private string GetWorkClassHelp(BackgroundTaskWorkClass workClass) =>
        L[$"WorkClassHelp_{workClass}"];

    private string GetTaskHelp(string taskName) =>
        L[$"TaskHelp_{taskName}"];
}
