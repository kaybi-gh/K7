using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace K7.Server.Web.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private Guid? _domainUserId;
    private bool _domainUserResolved;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    public string? IdentityId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(OpenIddictConstants.Claims.Subject);

    public Guid? Id
    {
        get
        {
            if (_domainUserResolved)
                return _domainUserId;

            _domainUserResolved = true;
            var identityId = IdentityId;
            if (string.IsNullOrEmpty(identityId))
                return null;

            var context = _serviceProvider.GetRequiredService<IApplicationDbContext>();
            _domainUserId = context.Users
                .AsNoTracking()
                .Where(u => u.IdentityUserId == identityId)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefault();

            return _domainUserId;
        }
    }
}
