namespace K7.Server.Application.Common.Exceptions;

/// <summary>
/// Raised when an external metadata provider returns HTTP 429. Carries the logical admission key and
/// the Retry-After delay so background-task scheduling can cool down that provider and align
/// <c>NextRetryAfter</c>.
/// </summary>
public sealed class ProviderRateLimitedException : Exception
{
    public ProviderRateLimitedException(string providerName, TimeSpan retryAfter, string? message = null)
        : base(message ?? $"Rate limited by {providerName}. Retry after {retryAfter.TotalSeconds:0.#}s.")
    {
        ProviderName = string.IsNullOrWhiteSpace(providerName)
            ? "unknown"
            : providerName.Trim().ToLowerInvariant();
        RetryAfter = retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(5);
    }

    public string ProviderName { get; }

    public TimeSpan RetryAfter { get; }
}
