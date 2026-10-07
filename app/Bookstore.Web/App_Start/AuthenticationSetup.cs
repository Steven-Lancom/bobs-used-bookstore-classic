using System.Security.Claims;
using BobsBookstoreClassic.Data;
using Bookstore.Domain.Customers;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bookstore.Web
{
    public static class AuthenticationSetup
    {
        public static void ConfigureAuthentication(WebApplicationBuilder builder, BookstoreConfiguration config)
        {
            if (config.GetSetting("Services/Authentication") == "aws")
            {
                ConfigureCognitoAuthentication(builder, config);
            }
            else
            {
                ConfigureLocalAuthentication(builder);
            }
        }

        private static void ConfigureLocalAuthentication(WebApplicationBuilder builder)
        {
            // Local auth uses a custom middleware that creates a ClaimsPrincipal
            // and sets a cookie. We add cookie auth as the default scheme so
            // the [Authorize] attribute / FallbackPolicy recognize the user.
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
        }

        private static void ConfigureCognitoAuthentication(WebApplicationBuilder builder, BookstoreConfiguration config)
        {
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.ClientId = config.GetSetting("Authentication/Cognito/LocalClientId");
                options.MetadataAddress = config.GetSetting("Authentication/Cognito/MetadataAddress");
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.UseTokenLifetime = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "cognito:username",
                    RoleClaimType = "cognito:groups"
                };

                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        var returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                        context.ProtocolMessage.RedirectUri = returnUrl;
                        return Task.CompletedTask;
                    },
                    OnAuthorizationCodeReceived = context =>
                    {
                        var returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                        context.TokenEndpointRequest!.RedirectUri = returnUrl;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();

                        var principal = new ClaimsPrincipal(context.Principal!.Identity!);
                        var identity = (ClaimsIdentity)principal.Identity!;

                        var dto = new CreateOrUpdateCustomerDto(
                            identity.GetSub(),
                            identity.Name!,
                            identity.FindFirst(y => y.Type.Contains("givenname"))!.Value,
                            identity.FindFirst(y => y.Type.Contains("surname"))!.Value);

                        await service.CreateOrUpdateCustomerAsync(dto);
                    }
                };
            });
        }
    }
}
