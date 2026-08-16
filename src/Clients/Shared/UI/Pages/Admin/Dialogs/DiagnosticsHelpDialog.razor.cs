using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Pages.Admin.Panels;
using K7.Server.Domain.Enums;
using K7.Shared.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class DiagnosticsHelpDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<AdminDiagnosticsPanel> PanelL { get; set; } = default!;

    private void Close() => Dialog.Cancel();

    private static readonly DiagnosticWorkClass[] WorkClassOrder =
    [
        DiagnosticWorkClass.Catalog,
        DiagnosticWorkClass.Enrichment,
        DiagnosticWorkClass.Polish
    ];

    private static IReadOnlyList<DiagnosticIssue> IssuesForWorkClass(DiagnosticWorkClass workClass) =>
        DiagnosticIssueTaxonomy.IssuesForWorkClass(workClass);

    private void NavigateToIssue(DiagnosticIssue issue)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["issue"] = DiagnosticIssueTaxonomy.Canonicalize(issue).ToString()
        };
        var href = Navigation.GetUriWithQueryParameters("/admin/diagnostics/details", parameters);
        Dialog.Close();
        Navigation.NavigateTo(href);
    }

    private string GetIssueLabel(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile => PanelL["OrphanFiles"],
        DiagnosticIssue.UnidentifiedFile => PanelL["UnidentifiedFiles"],
        DiagnosticIssue.MissingFileMetadata => PanelL["MissingFileMetadata"],
        DiagnosticIssue.MissingHlsSegments => PanelL["MissingHlsSegments"],
        DiagnosticIssue.MissingChapters => PanelL["MissingChapters"],
        DiagnosticIssue.MissingThemeSong => PanelL["MissingThemeSong"],
        DiagnosticIssue.MissingIntroOutro => PanelL["MissingIntroOutro"],
        DiagnosticIssue.MissingPictures => PanelL["MissingPictures"],
        DiagnosticIssue.MissingMetadata => PanelL["MissingMetadata"],
        DiagnosticIssue.MissingExternalId => PanelL["MissingExternalId"],
        DiagnosticIssue.StaleMetadata => PanelL["StaleMetadata"],
        DiagnosticIssue.MissingAudioAnalysis => PanelL["MissingAudioAnalysis"],
        DiagnosticIssue.MissingFiles => PanelL["MissingFiles"],
        DiagnosticIssue.InaccessiblePath => PanelL["InaccessiblePath"],
        DiagnosticIssue.MissingMembers => PanelL["MissingMembers"],
        DiagnosticIssue.DuplicateExternalId => PanelL["DuplicateExternalId"],
        DiagnosticIssue.SuspectedDuplicateMedia => PanelL["SuspectedDuplicateMedia"],
        _ => issue.ToString()
    };

    private string GetAppliesToLabel(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.GetEntityType(DiagnosticIssueTaxonomy.Canonicalize(issue)) switch
        {
            DiagnosticEntityType.IndexedFile => PanelL["AppliesToIndexedFile"],
            DiagnosticEntityType.Library => PanelL["AppliesToLibrary"],
            _ => PanelL["AppliesToMedia"]
        };

    private string GetWorkClassLabel(DiagnosticWorkClass workClass) => workClass switch
    {
        DiagnosticWorkClass.Catalog => PanelL["WorkClassCatalog"],
        DiagnosticWorkClass.Enrichment => PanelL["WorkClassEnrichment"],
        DiagnosticWorkClass.Polish => PanelL["WorkClassPolish"],
        _ => workClass.ToString()
    };

    private string GetSeverityShortLabel(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssueTaxonomy.Canonicalize(issue)) switch
        {
            DiagnosticSeverity.Error => L["SeverityError"],
            DiagnosticSeverity.Info => L["SeverityInfo"],
            _ => L["SeverityWarning"]
        };

    private string GetSeverityLabel(DiagnosticIssue issue) =>
        GetSeverityShortLabel(issue);

    private string GetDefinition(DiagnosticIssue issue) =>
        L[$"Def_{DiagnosticIssueTaxonomy.Canonicalize(issue)}"];

    private string GetRemediationExplanation(DiagnosticIssue issue)
    {
        var canonical = DiagnosticIssueTaxonomy.Canonicalize(issue);
        if (DiagnosticIssueTaxonomy.SupportsBulkFix(canonical))
            return L[$"Bulk_{canonical}"];

        return L[$"Manual_{canonical}"];
    }
}