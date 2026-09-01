namespace K7.Server.Web.Infrastructure;

/// <summary>
/// HTTP paths for Docker/K8s probes. Liveness is process-up only. Readiness includes DB.
/// </summary>
internal static class HealthProbePaths
{
    public const string Readiness = "/health";
    public const string Liveness = "/alive";
    public const string LiveTag = "live";
}
