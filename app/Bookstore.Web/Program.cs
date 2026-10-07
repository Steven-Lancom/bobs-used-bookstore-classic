using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using BobsBookstoreClassic.Data;
using Bookstore.Common;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────
BookstoreConfiguration.Initialize(builder.Configuration);

// Load additional settings from AWS Systems Manager Parameter Store if configured
LoadAwsConfiguration();

// ── Logging ────────────────────────────────────────────────────────────────
ConfigureNLog();
builder.Logging.ClearProviders();
builder.Logging.AddNLog();

// ── Database ───────────────────────────────────────────────────────────────
var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── ASP.NET Core MVC ───────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// ── Authentication ─────────────────────────────────────────────────────────
ConfigureAuthentication(builder.Services);

// ── Dependency Injection ───────────────────────────────────────────────────
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// File service
if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService, S3FileService>();
}
else
{
    builder.Services.AddSingleton<IFileService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        return new LocalFileService(env.WebRootPath);
    });
}

// Image validation service
if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// Local auth middleware (only when not using Cognito)
if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
{
    builder.Services.AddScoped<LocalAuthenticationMiddleware>();
}

// ── Http Context Accessor ──────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Ensure database is up to date ──────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

// ── Middleware pipeline ─────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();

// ── Routes ──────────────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ─── Helper methods ──────────────────────────────────────────────────────────

void LoadAwsConfiguration()
{
    var rootPath = "/" + Constants.AppName;

    if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParameterRequest
        {
            Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
        };
        var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
        BookstoreConfiguration.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest
        {
            Path = $"{rootPath}/Authentication/",
            Recursive = true
        };
        var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
        foreach (var parameter in response.Parameters)
        {
            BookstoreConfiguration.AddSetting(
                parameter.Name.Replace($"{rootPath}/", string.Empty),
                parameter.Value);
        }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest
        {
            Path = $"{rootPath}/Files/",
            Recursive = true
        };
        var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
        foreach (var parameter in response.Parameters)
        {
            BookstoreConfiguration.AddSetting(
                parameter.Name.Replace($"{rootPath}/", string.Empty),
                parameter.Value);
        }
    }
}

void ConfigureNLog()
{
    var config = new LoggingConfiguration();
    NLog.Targets.Target loggingTarget;

    if (BookstoreConfiguration.GetSetting("Services/LoggingService") == "aws")
    {
        loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
    }
    else
    {
        loggingTarget = new DebuggerTarget();
    }

    config.AddTarget("main", loggingTarget);
    config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));
    LogManager.Configuration = config;
}

void ConfigureAuthentication(IServiceCollection services)
{
    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.ClientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
            options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.SaveTokens = true;
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
                    var request = context.Request;
                    context.ProtocolMessage.RedirectUri =
                        $"{request.Scheme}://{request.Host}/signin-oidc";
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var service = context.HttpContext.RequestServices
                        .GetRequiredService<ICustomerService>();

                    var identity = (ClaimsIdentity)context.Principal!.Identity!;

                    var dto = new CreateOrUpdateCustomerDto(
                        identity.GetSub()!,
                        identity.Name ?? string.Empty,
                        identity.FindFirst(c => c.Type.Contains("givenname"))?.Value ?? string.Empty,
                        identity.FindFirst(c => c.Type.Contains("surname"))?.Value ?? string.Empty);

                    await service.CreateOrUpdateCustomerAsync(dto);
                }
            };
        });
    }
    else
    {
        // Local auth — authentication is handled by LocalAuthenticationMiddleware
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();
    }

    services.AddAuthorization();
}
