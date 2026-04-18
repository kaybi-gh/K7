using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Interfaces;

public interface IK7DialogService
{
    Task<IK7DialogReference> ShowAsync<TDialog>(
        string title,
        K7DialogParameters? parameters = null,
        K7DialogOptions? options = null)
        where TDialog : ComponentBase;

    Task<IK7DialogReference> ShowAsync(
        Type dialogType,
        string title,
        K7DialogParameters? parameters = null,
        K7DialogOptions? options = null);
}
