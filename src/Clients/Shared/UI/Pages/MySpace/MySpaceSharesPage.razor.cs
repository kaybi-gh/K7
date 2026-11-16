using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceSharesPage
{
    /// <summary>Share whose sheet is open. Null shows the list.</summary>
    [Parameter] public Guid? ShareId { get; set; }
}
