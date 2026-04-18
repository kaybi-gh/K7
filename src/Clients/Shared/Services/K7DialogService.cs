using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Services;

public sealed class K7DialogService : IK7DialogService
{
    public event Func<K7DialogRequest, Task<IK7DialogReference>>? OnShow;

    public Task<IK7DialogReference> ShowAsync<TDialog>(
        string title,
        K7DialogParameters? parameters = null,
        K7DialogOptions? options = null)
        where TDialog : ComponentBase
        => ShowAsync(typeof(TDialog), title, parameters, options);

    public Task<IK7DialogReference> ShowAsync(
        Type dialogType,
        string title,
        K7DialogParameters? parameters = null,
        K7DialogOptions? options = null)
    {
        if (OnShow is null) throw new InvalidOperationException("K7DialogHost is not registered in the component tree.");
        return OnShow(new K7DialogRequest
        {
            Type = dialogType,
            Title = title,
            Parameters = parameters,
            Options = options
        });
    }
}
