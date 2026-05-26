using System.Net.Http.Json;
using System.Text.Json;

namespace K7.Shared.Extensions;

public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task EnsureSuccessWithDetailsAsync(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = await TryReadProblemDetailAsync(response, cancellationToken);
        if (!string.IsNullOrEmpty(detail))
            throw new HttpRequestException(detail, null, response.StatusCode);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsLite>(JsonOptions, cancellationToken);
            if (problem is null)
                return null;

            if (problem.Errors is { Count: > 0 })
            {
                var messages = problem.Errors
                    .SelectMany(kvp => kvp.Value)
                    .Where(m => !string.IsNullOrWhiteSpace(m));
                return string.Join(Environment.NewLine, messages);
            }

            return problem.Detail ?? problem.Title;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ProblemDetailsLite
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
