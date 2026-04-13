using System.Net;
using OpenIddict.Client;
using OpenIddict.Client.SystemIntegration;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Client.OpenIddictClientEvents;

namespace K7.Clients.MAUI.Services.Authentication;

internal static class LoginCallbackResponseHandler
{
    public static OpenIddictClientHandlerDescriptor Descriptor { get; }
        = OpenIddictClientHandlerDescriptor.CreateBuilder<ApplyRedirectionResponseContext>()
            .AddFilter<OpenIddictClientSystemIntegrationHandlerFilters.RequireHttpListenerContext>()
            .UseInlineHandler(static context =>
            {
                var response = context.Transaction.GetHttpListenerContext()?.Response;
                if (response is null)
                {
                    return default;
                }

                var issuer = context.Registration?.Issuer?.ToString().TrimEnd('/');
                if (string.IsNullOrEmpty(issuer))
                {
                    return default;
                }

                var status = context.Response.Error switch
                {
                    null or { Length: 0 } => "success",
                    Errors.AccessDenied => "denied",
                    _ => "error"
                };

                response.Redirect($"{issuer}/auth/complete?status={status}");
                response.Close();

                context.HandleRequest();
                return default;
            })
            .SetOrder(int.MaxValue - 100_001)
            .Build();
}
