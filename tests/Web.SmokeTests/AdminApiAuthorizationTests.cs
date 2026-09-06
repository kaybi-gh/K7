using K7.Server.Domain.Constants;
using K7.Tests.Helpers.Smoke;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class AdminApiAuthorizationTests
{
    private SmokeWebApplicationFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new SmokeWebApplicationFactory();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public void AdminApiRoutes_ShouldRequireAdminOnly()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var adminRoutes = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => IsAdminApiRoute(endpoint.RoutePattern.RawText))
            .ToArray();

        adminRoutes.Should().NotBeEmpty();

        foreach (var endpoint in adminRoutes)
        {
            var display = $"{endpoint.DisplayName} ({endpoint.RoutePattern.RawText})";

            endpoint.Metadata.GetMetadata<IAllowAnonymous>()
                .Should().BeNull("{0} must not allow anonymous access", display);

            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => !string.IsNullOrEmpty(policy))
                .ToHashSet(StringComparer.Ordinal);

            policies.Should().Contain(
                Policies.AdminOnly,
                $"{display} must require {Policies.AdminOnly}");
        }
    }

    private static bool IsAdminApiRoute(string? template)
    {
        if (string.IsNullOrEmpty(template))
            return false;

        var path = template.TrimStart('~').Trim();
        return path.Equals("/api/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase);
    }
}
