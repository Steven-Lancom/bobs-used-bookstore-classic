using BobsBookstoreClassic.Data;
using Bookstore.Web;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// --- BookstoreConfiguration (wraps IConfiguration + SSM overrides) ---
var bookstoreConfig = new BookstoreConfiguration(builder.Configuration);
builder.Services.AddSingleton(bookstoreConfig);

// --- Logging (NLog) ---
LoggingSetup.ConfigureLogging(bookstoreConfig);
builder.Host.UseNLog();

// --- Configuration (AWS SSM Parameter Store) ---
await ConfigurationSetup.ConfigureConfigurationAsync(bookstoreConfig);

// --- MVC ---
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// --- DI (all services, repos, DbContext, AWS clients) ---
DependencyInjectionSetup.ConfigureDependencyInjection(builder, bookstoreConfig);

// --- Authentication (Cognito OIDC or Local middleware) ---
AuthenticationSetup.ConfigureAuthentication(builder, bookstoreConfig);

// --- Authorization (global [Authorize] equivalent via FallbackPolicy) ---
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// --- Middleware pipeline ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// Local auth middleware must run after UseAuthentication but before UseAuthorization
// so that the ClaimsPrincipal it creates is recognized by the authorization system.
if (bookstoreConfig.GetSetting("Services/Authentication") != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthorization();

// --- Endpoint routing ---
app.MapControllerRoute(
    name: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
