using System.Reflection;
using System.Text.RegularExpressions;

namespace K7.Server.Web.Infrastructure;

public static partial class WebApplicationExtensions
{
    public static RouteGroupBuilder MapGroup(this WebApplication app, EndpointGroupBase group)
    {
        var groupName = group.GetType().Name;

        return app
            .MapGroup($"/api/{groupName.PascalToKebabCase()}")
            .WithGroupName(groupName)
            .WithTags(groupName)
            .WithOpenApi();
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var endpointGroupType = typeof(EndpointGroupBase);

        var assembly = Assembly.GetExecutingAssembly();

        var endpointGroupTypes = assembly.GetExportedTypes()
            .Where(t => t.IsSubclassOf(endpointGroupType));

        foreach (var type in endpointGroupTypes)
        {
            if (Activator.CreateInstance(type) is EndpointGroupBase instance)
            {
                instance.Map(app);
            }
        }

        return app;
    }

    private static string PascalToKebabCase(this string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : PascalSpaces()
                .Replace(value, "-$1")
                .Trim()
                .ToLower();
    }

    [GeneratedRegex("(?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z0-9])", RegexOptions.Compiled)]
    private static partial Regex PascalSpaces();
}
