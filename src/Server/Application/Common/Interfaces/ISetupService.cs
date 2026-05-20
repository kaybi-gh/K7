using K7.Server.Application.Common.Models;

namespace K7.Server.Application.Common.Interfaces;

public interface ISetupService
{
    Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default);
    Task<Result> CompleteSetupAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result> CompleteSetupWithExternalLoginAsync(string email, string loginProvider, string providerKey, CancellationToken cancellationToken = default);
}
