using System.Reflection;
using K7.Clients.Shared.UI.Pages.Utils;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.ComponentTests.Pages;

[TestFixture]
public class AdminRouteAuthorizationTests
{
    [Test]
    public void AdminPages_ShouldRequireAdministratorRole()
    {
        var pages = typeof(ISharedPagesPointer).Assembly
            .GetTypes()
            .Select(type => (
                Type: type,
                Routes: type.GetCustomAttributes<RouteAttribute>(inherit: true)
                    .Select(route => route.Template)
                    .Where(IsAdminRoute)
                    .ToArray()))
            .Where(page => page.Routes.Length > 0)
            .ToArray();

        pages.Should().NotBeEmpty();

        foreach (var (type, routes) in pages)
        {
            type.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
                .Should().BeEmpty("{0} ({1}) must not allow anonymous access", type.FullName, string.Join(", ", routes));

            var roles = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet(StringComparer.Ordinal);

            roles.Should().Contain(
                Roles.Administrator,
                $"{type.FullName} ({string.Join(", ", routes)}) must require {Roles.Administrator}");
        }
    }

    private static bool IsAdminRoute(string? template)
    {
        if (string.IsNullOrEmpty(template))
            return false;

        var path = template.TrimStart('~').Trim();
        return path.Equals("/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("admin/", StringComparison.OrdinalIgnoreCase);
    }
}
