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
using NLog.Web;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Initialize BookstoreConfiguration from appsettings
    BookstoreConfiguration.Initialize(builder.Configuration);

    // Load AWS SSM parameters if configured
    await LoadAwsConfigurationAsync(builder.Configuration);

    // Configure logging
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Add MVC with Razor views
    builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

    // Configure EF Core
    var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection")
        ?? builder.Configuration.GetConnectionString("BookstoreDatabaseConnection");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Register services
    builder.Services.AddScoped<IBookService, BookService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddScoped<IOfferService, OfferService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
    builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

    // Register repositories
    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IAddressRepository, AddressRepository>();
    builder.Services.AddScoped<IBookRepository, BookRepository>();
    builder.Services.AddScoped<IOfferRepository, OfferRepository>();
    builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

    // File service
    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
        builder.Services.AddScoped<IFileService, S3FileService>();
    }
    else
    {
        builder.Services.AddScoped<IFileService>(sp =>
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

    // Authentication
    ConfigureAuthentication(builder.Services);

    builder.Services.AddHttpContextAccessor();

    var app = builder.Build();

    // Seed database
    await SeedDatabaseAsync(app);

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Admin/Error/Index/500");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Serve legacy /Content path from wwwroot
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // Local authentication middleware (for non-AWS auth)
    if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    app.MapAreaControllerRoute(
        name: "admin_area",
        areaName: "Admin",
        pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application startup failed");
    throw;
}
finally
{
    LogManager.Shutdown();
}

static void ConfigureAuthentication(IServiceCollection services)
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
                    context.ProtocolMessage.RedirectUri = GetReturnUrl(context.Request);
                    return Task.CompletedTask;
                },
                OnAuthorizationCodeReceived = context =>
                {
                    context.TokenEndpointRequest!.RedirectUri = GetReturnUrl(context.Request);
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                    var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                    if (identity == null) return;

                    var dto = new CreateOrUpdateCustomerDto(
                        identity.GetSub(),
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
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/Authentication/Login";
            });

        services.AddScoped<LocalAuthenticationMiddleware>();
    }
}

static string GetReturnUrl(HttpRequest request)
{
    return $"{request.Scheme}://{request.Host}/signin-oidc";
}

static async Task LoadAwsConfigurationAsync(ConfigurationManager configuration)
{
    BookstoreConfiguration.Initialize(configuration);

    var rootPath = "/" + Constants.AppName;

    if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParameterRequest
            {
                Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
            };
            var response = await client.GetParameterAsync(request);
            BookstoreConfiguration.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
        }
        catch (Exception) { /* SSM not available in local dev */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest
            {
                Path = $"{rootPath}/Authentication/",
                Recursive = true
            };
            var response = await client.GetParametersByPathAsync(request);
            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(
                    parameter.Name.Replace($"{rootPath}/", string.Empty),
                    parameter.Value);
            }
        }
        catch (Exception) { /* SSM not available in local dev */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest
            {
                Path = $"{rootPath}/Files/",
                Recursive = true
            };
            var response = await client.GetParametersByPathAsync(request);
            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(
                    parameter.Name.Replace($"{rootPath}/", string.Empty),
                    parameter.Value);
            }
        }
        catch (Exception) { /* SSM not available in local dev */ }
    }
}

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    await BookstoreDbSeeder.SeedAsync(db);
}
