using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware : IMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private const string CookieName = "LocalAuthentication";

        private readonly ICustomerService _customerService;

        public LocalAuthenticationMiddleware(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                var principal = CreateClaimsPrincipal();
                context.User = principal;

                await SaveCustomerDetailsAsync(context);

                context.Response.Cookies.Append(CookieName, "true", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(1),
                    HttpOnly = true
                });

                context.Response.Redirect("/");
            }
            else if (context.Request.Cookies[CookieName] != null)
            {
                context.User = CreateClaimsPrincipal();

                await SaveCustomerDetailsAsync(context);

                await next(context);
            }
            else
            {
                await next(context);
            }
        }

        private ClaimsPrincipal CreateClaimsPrincipal()
        {
            var identity = new ClaimsIdentity("Application");

            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));

            return new ClaimsPrincipal(identity);
        }

        private async Task SaveCustomerDetailsAsync(HttpContext context)
        {
            var identity = (ClaimsIdentity)context.User.Identity;

            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")?.Value ?? UserId,
                identity.Name ?? "bookstoreuser",
                identity.FindFirst("given_name")?.Value ?? "Bookstore",
                identity.FindFirst("family_name")?.Value ?? "User");

            await _customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
