using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.Database.Context.Services;

public sealed class DatabaseCapabilities(IOptions<DatabaseConfiguration> options) : IDatabaseCapabilities
{
    public bool SupportsTrigramSearch =>
        string.Equals(options.Value.Provider, "postgres", StringComparison.OrdinalIgnoreCase);
}
