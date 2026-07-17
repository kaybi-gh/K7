using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace K7.Server.Web.Services;

public class CurrentUser : IUser
{
    public const string SharedProfileHeaderName = K7.Shared.HttpHeaderNames.SharedProfileId;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private Guid? _domainUserId;
    private bool _domainUserResolved;
    private Guid? _sharedProfileId;
    private bool _sharedProfileResolved;

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

            // Compatibility path for synchronous callers. New asynchronous code should use GetIdAsync.
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

    public Guid? SharedProfileId
    {
        get
        {
            if (_sharedProfileResolved)
                return _sharedProfileId;

            _sharedProfileResolved = true;
            _sharedProfileId = ResolveSharedProfileIdSync();
            return _sharedProfileId;
        }
    }

    public async Task<Guid?> GetIdAsync(CancellationToken cancellationToken = default)
    {
        if (_domainUserResolved)
            return _domainUserId;

        var identityId = IdentityId;
        if (string.IsNullOrEmpty(identityId))
        {
            _domainUserResolved = true;
            return null;
        }

        var context = _serviceProvider.GetRequiredService<IApplicationDbContext>();
        var domainUserId = await context.Users
            .AsNoTracking()
            .Where(u => u.IdentityUserId == identityId)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _domainUserId = domainUserId;
        _domainUserResolved = true;
        return _domainUserId;
    }

    public async Task<Guid?> GetSharedProfileIdAsync(CancellationToken cancellationToken = default)
    {
        if (_sharedProfileResolved)
            return _sharedProfileId;

        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers[SharedProfileHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue) || !Guid.TryParse(headerValue, out var requestedId))
        {
            _sharedProfileResolved = true;
            return null;
        }

        var userId = await GetIdAsync(cancellationToken);
        if (userId is null)
        {
            _sharedProfileResolved = true;
            return null;
        }

        var context = _serviceProvider.GetRequiredService<IApplicationDbContext>();
        var isAllowed = await context.SharedProfiles
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == requestedId
                     && (p.HostUserId == userId.Value
                         || p.Members.Any(m => m.UserId == userId.Value)),
                cancellationToken);

        _sharedProfileId = isAllowed ? requestedId : null;
        _sharedProfileResolved = true;
        return _sharedProfileId;
    }

    private Guid? ResolveSharedProfileIdSync()
    {
        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers[SharedProfileHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue) || !Guid.TryParse(headerValue, out var requestedId))
            return null;

        var userId = Id;
        if (userId is null)
            return null;

        var context = _serviceProvider.GetRequiredService<IApplicationDbContext>();
        var isAllowed = context.SharedProfiles
            .AsNoTracking()
            .Any(p => p.Id == requestedId
                      && (p.HostUserId == userId.Value
                          || p.Members.Any(m => m.UserId == userId.Value)));

        return isAllowed ? requestedId : null;
    }
}
