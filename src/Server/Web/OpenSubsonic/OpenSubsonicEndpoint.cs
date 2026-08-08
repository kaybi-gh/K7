using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetDirectStream;
using K7.Server.Application.Features.OpenSubsonic;
using K7.Server.Application.Services;
using K7.Server.Web.Endpoints;
using K7.Shared.Json;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.OpenSubsonic;

public sealed class OpenSubsonicEndpoint : IEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapMethods("/rest/{*actionPath}", ["GET", "POST"], HandleAsync)
            .AllowAnonymous()
            .WithName("OpenSubsonic")
            .WithTags("OpenSubsonic");
    }

    private static async Task<IResult> HandleAsync(
        string actionPath,
        HttpContext httpContext,
        [FromServices] OpenSubsonicAuthenticator authenticator,
        [FromServices] IOpenSubsonicService openSubsonicService,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        IFormCollection? form = null;
        if (HttpMethods.IsPost(httpContext.Request.Method)
            && httpContext.Request.HasFormContentType)
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }

        var auth = await authenticator.AuthenticateAsync(httpContext.Request.Query, form, cancellationToken);
        if (auth.IsFailed)
            return WriteEnvelope(httpContext, auth.Error!, GetFormat(httpContext.Request.Query, form));

        httpContext.User = auth.Principal!;

        var parameters = ToParamDictionary(httpContext.Request.Query, form);
        var action = NormalizeActionPath(actionPath);
        var result = await openSubsonicService.ExecuteAsync(
            action,
            parameters,
            auth.Username,
            auth.CanWrite,
            cancellationToken);

        if (result.IsBinary && result.Binary is not null)
            return await WriteBinaryAsync(httpContext, result.Binary, sender, cancellationToken);

        if (result.IsFailed)
            return WriteEnvelope(httpContext, result.Error!, GetFormat(parameters));

        return WriteEnvelope(httpContext, data: result.Data, format: GetFormat(parameters));
    }

    private static async Task<IResult> WriteBinaryAsync(
        HttpContext httpContext,
        OpenSubsonicBinaryPayload binary,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (binary.TransferSessionId is { } transferSessionId
            && binary.TransferMediaId is { } transferMediaId)
        {
            var tracker = httpContext.RequestServices.GetService<IActiveStreamTracker>();
            if (tracker is not null)
            {
                tracker.BeginOpenSubsonicTransfer(transferSessionId, transferMediaId);
                httpContext.Response.OnCompleted(() =>
                {
                    tracker.EndOpenSubsonicTransfer(transferSessionId, transferMediaId);
                    return Task.CompletedTask;
                });
            }
        }

        if (binary.OpenStream is not null)
        {
            return Results.File(
                binary.OpenStream(),
                contentType: binary.ContentType ?? "application/octet-stream",
                fileDownloadName: binary.FileDownloadName);
        }

        if (binary.IndexedFileId is { } indexedFileId)
        {
            var content = await sender.Send(new GetDirectStreamQuery(indexedFileId), cancellationToken);
            if (content is FileHttpContentResult file)
            {
                return Results.File(
                    file.FilePath,
                    contentType: file.ContentType,
                    fileDownloadName: binary.FileDownloadName ?? file.FileDownloadName,
                    enableRangeProcessing: binary.EnableRangeProcessing && file.EnableRangeProcessing);
            }

            return content.ToIResult();
        }

        if (string.IsNullOrWhiteSpace(binary.FilePath) || !File.Exists(binary.FilePath))
            return WriteEnvelope(null, new OpenSubsonicError
            {
                Code = OpenSubsonicConstants.ErrorNotFound,
                Message = "File not found."
            }, "json");

        return Results.File(
            binary.FilePath,
            contentType: binary.ContentType ?? "application/octet-stream",
            fileDownloadName: binary.FileDownloadName,
            enableRangeProcessing: binary.EnableRangeProcessing);
    }

    private static IResult WriteEnvelope(
        HttpContext? httpContext,
        OpenSubsonicError? error = null,
        string format = "json",
        IReadOnlyDictionary<string, object?>? data = null)
    {
        var serverVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";

        var body = new Dictionary<string, object?>
        {
            ["status"] = error is null ? "ok" : "failed",
            ["version"] = OpenSubsonicConstants.ProtocolVersion,
            ["type"] = OpenSubsonicConstants.ServerType,
            ["serverVersion"] = serverVersion,
            ["openSubsonic"] = true
        };

        if (error is not null)
            body["error"] = error;

        if (data is not null)
        {
            foreach (var pair in data)
                body[pair.Key] = pair.Value;
        }

        var envelope = new Dictionary<string, object?>
        {
            ["subsonic-response"] = body
        };

        if (string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = ToXml(body);
            return Results.Content(xml, "text/xml; charset=utf-8");
        }

        return Results.Json(envelope, JsonOptions);
    }

    private static string ToXml(Dictionary<string, object?> body)
    {
        var root = new XElement("subsonic-response",
            new XAttribute("status", body["status"]?.ToString() ?? "ok"),
            new XAttribute("version", OpenSubsonicConstants.ProtocolVersion),
            new XAttribute("type", OpenSubsonicConstants.ServerType),
            new XAttribute("serverVersion", body["serverVersion"]?.ToString() ?? "0.0.0"),
            new XAttribute("openSubsonic", "true"));

        if (body.TryGetValue("error", out var errorObj) && errorObj is OpenSubsonicError error)
        {
            var errorElement = new XElement("error",
                new XAttribute("code", error.Code),
                new XAttribute("message", error.Message));
            if (!string.IsNullOrEmpty(error.HelpUrl))
                errorElement.SetAttributeValue("helpUrl", error.HelpUrl);
            root.Add(errorElement);
        }

        // Prefer JSON for rich payloads; XML returns a minimal valid envelope.
        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToString();
    }

    private static Dictionary<string, string[]> ToParamDictionary(IQueryCollection query, IFormCollection? form)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        void Add(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> source)
        {
            foreach (var pair in source)
            {
                var values = pair.Value.Where(v => v is not null).Select(v => v!).ToArray();
                if (values.Length == 0)
                    continue;

                if (result.TryGetValue(pair.Key, out var existing))
                    result[pair.Key] = existing.Concat(values).ToArray();
                else
                    result[pair.Key] = values;
            }
        }

        Add(query);
        if (form is not null)
            Add(form);

        return result;
    }

    private static string NormalizeActionPath(string actionPath)
    {
        var action = actionPath.Trim().Trim('/');
        var slash = action.LastIndexOf('/');
        if (slash >= 0)
            action = action[(slash + 1)..];
        return action;
    }

    private static string GetFormat(IQueryCollection query, IFormCollection? form)
    {
        var f = query["f"].FirstOrDefault()
            ?? form?["f"].FirstOrDefault()
            ?? "json";
        return f;
    }

    private static string GetFormat(IReadOnlyDictionary<string, string[]> parameters)
    {
        if (parameters.TryGetValue("f", out var values) && values.Length > 0)
            return values[0];
        return "json";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = K7JsonSerializerOptions.CreateDefault();
        options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        return options;
    }
}
