using Microsoft.AspNetCore.Components;

namespace K7.Server.Web.Components.Account
{
    internal sealed class IdentityRedirectManager(
        NavigationManager navigationManager,
        IHttpContextAccessor httpContextAccessor)
    {
        public const string StatusCookieName = "Identity.StatusMessage";

        private static readonly CookieBuilder StatusCookieBuilder = new()
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromSeconds(5),
        };

        public void RedirectTo(string? uri)
        {
            uri = NormalizeRedirectUri(uri);

            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is not null && !httpContext.Response.HasStarted)
            {
                httpContext.Response.Redirect(uri);
                return;
            }

            // forceLoad ensures auth cookies from the POST response are visible on the next request.
            navigationManager.NavigateTo(uri, forceLoad: true);
            throw new InvalidOperationException($"{nameof(IdentityRedirectManager)} can only be used during static rendering.");
        }

        public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
        {
            var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
            var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
            RedirectTo(newUri);
        }

        public void RedirectToWithStatus(string uri, string message, HttpContext context)
        {
            context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));

            uri = NormalizeRedirectUri(uri);
            if (!context.Response.HasStarted)
            {
                context.Response.Redirect(uri);
                return;
            }

            RedirectTo(uri);
        }

        private string NormalizeRedirectUri(string? uri) =>
            IdentityRedirectUri.Normalize(uri, navigationManager.ToBaseRelativePath);

        private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

        public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

        public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
            => RedirectToWithStatus(CurrentPath, message, context);
    }
}
