using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Interfaces;

public interface IK7DialogInstance
{
    string Title { get; }
    K7DialogOptions? Options { get; }
    RenderFragment? HeaderActions { get; }
    void SetTitle(string title);
    void SetHeaderActions(RenderFragment? actions);
    void Close();
    void Close(K7DialogResult result);
    void Cancel();
}
