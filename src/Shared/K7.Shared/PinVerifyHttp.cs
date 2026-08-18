namespace K7.Shared;

public static class PinVerifyHttp
{
    public static bool IsPinVerifyRequest(Uri? requestUri)
    {
        if (requestUri is null)
            return false;

        var path = requestUri.IsAbsoluteUri ? requestUri.AbsolutePath : requestUri.OriginalString;
        return path.EndsWith("/verify-pin", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("verify-pin", StringComparison.OrdinalIgnoreCase);
    }
}
