using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// Release builds had no sink for Blazor errors. An exception outside an ErrorBoundary (dialog
/// host, snackbar host, layout chrome) can leave the WebView without .NET event handling while
/// the JS focus navigation keeps working, which looks like "OK does nothing anywhere". This
/// provider forwards Blazor error logs to logcat (K7-Error), to PerfTrace when measurements are
/// on, and to the server error list (once per minute per error kind).
/// </summary>
public sealed class BlazorErrorLoggerProvider(IServiceProvider services) : ILoggerProvider
{
    private const string CategoryPrefix = "Microsoft.AspNetCore.Components";
    private const int MaxMessageLength = 300;
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMinutes(1);
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _lastReports = new(StringComparer.Ordinal);

    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(CategoryPrefix, StringComparison.Ordinal)
            ? new ErrorLogger(this, categoryName)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private void Forward(string category, string message, Exception? exception)
    {
        var kind = exception?.GetType().Name ?? "Error";
        var text = message.Length > MaxMessageLength ? message[..MaxMessageLength] : message;
        var line = "blazor " + kind + " in " + category + ": " + text;

        MauiStartupTrace.WriteError(line);
        PerfTrace.Note(line);

        if (!ShouldReport(kind + "|" + category))
            return;

        try
        {
            services.GetService<IClientErrorReporter>()?.ReportError(
                exception ?? new InvalidOperationException(text),
                "Blazor " + category,
                notifyUser: false);
        }
        catch
        {
            // Error reporting must never throw back into the renderer.
        }
    }

    private bool ShouldReport(string key)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_lastReports.TryGetValue(key, out var last) && now - last < ReportInterval)
                return false;

            _lastReports[key] = now;
            return true;
        }
    }

    private sealed class ErrorLogger(BlazorErrorLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            owner.Forward(category, formatter(state, exception), exception);
        }
    }
}
