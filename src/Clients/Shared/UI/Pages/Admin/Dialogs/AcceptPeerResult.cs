namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public sealed record AcceptPeerResult(List<Guid> LibraryIds, bool AutoShareNewLibraries);
