using System.Security.Claims;
using System.Security.Principal;

namespace Bookstore.Web.Helpers
{
    /// <summary>
    /// Extension for OIDC redirect URI generation in ASP.NET Core.
    /// </summary>
    public static class OwinRequestExtensions
    {
        public static string GetReturnUrl(this Microsoft.AspNetCore.Http.HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host}/signin-oidc";
        }
    }
}
