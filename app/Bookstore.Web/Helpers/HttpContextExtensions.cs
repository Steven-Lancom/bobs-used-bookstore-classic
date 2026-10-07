using Microsoft.AspNetCore.Http;
using System;

namespace Bookstore.Web.Helpers
{
    public static class HttpContextExtensions
    {
        public static string GetShoppingCartCorrelationId(this HttpContext context)
        {
            const string CookieKey = "ShoppingCartId";

            string shoppingCartClientId = context.Request.Cookies[CookieKey];

            if (string.IsNullOrWhiteSpace(shoppingCartClientId))
            {
                shoppingCartClientId = context.User?.Identity?.IsAuthenticated == true
                    ? context.User.GetSub()
                    : Guid.NewGuid().ToString();
            }

            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/"
            };

            context.Response.Cookies.Append(CookieKey, shoppingCartClientId, cookieOptions);

            return shoppingCartClientId;
        }
    }
}
