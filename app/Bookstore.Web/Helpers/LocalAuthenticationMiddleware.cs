using System.Security.Claims;
using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware : IMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private readonly ICustomerService customerService;

        public LocalAuthenticationMiddleware(ICustomerService customerService)
        {
            this.customerService = customerService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                var principal = CreateClaimsPrincipal();
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1) });

                context.User = principal;
                await SaveCustomerDetailsAsync(principal);

                context.Response.Redirect("/");
            }
            else if (context.Request.Cookies.ContainsKey("LocalAuthentication") || context.User.Identity?.IsAuthenticated == true)
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    var principal = CreateClaimsPrincipal();
                    context.User = principal;
                    await SaveCustomerDetailsAsync(principal);
                }
                await next(context);
            }
            else
            {
                await next(context);
            }
        }

        private static ClaimsPrincipal CreateClaimsPrincipal()
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));
            return new ClaimsPrincipal(identity);
        }

        private async Task SaveCustomerDetailsAsync(ClaimsPrincipal principal)
        {
            var identity = (ClaimsIdentity)principal.Identity!;
            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")!.Value,
                identity.Name!,
                identity.FindFirst("given_name")!.Value,
                identity.FindFirst("family_name")!.Value);
            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
