using System.Reflection;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace K7.Server.Web;

public static class OpenApiSetup
{
    public static bool IsRequested => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static void RunOpenApiGeneration(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpoints();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi(o =>
        {
            o.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
            o.AddScalarTransformers();
            o.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "K7 Server API";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            });
        });

        var app = builder.Build();
        app.MapEndpoints();
        app.Run();
    }
}
