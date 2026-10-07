# Migration Summary: .NET Framework 4.8 → .NET 10

## Build Status

```
dotnet build BobsBookstoreClassic.sln
  → Build succeeded.  0 Error(s)
```

## What Was Migrated

### Project Files

| Project | Before | After | Notes |
|---|---|---|---|
| `Bookstore.Web` | Legacy .csproj, TF 4.8 | SDK-style, `net10.0`, `Microsoft.NET.Sdk.Web` | Full web app migration |
| `Bookstore.Data` | SDK-style, `netstandard2.0` | SDK-style, `net10.0` | EF6 → EF Core, single TFM (uses EF Core SQL Server which requires net8.0+) |
| `Bookstore.Domain` | SDK-style, `netstandard2.0` | SDK-style, `net10.0;netstandard2.0` | Multi-targeted; no platform-specific deps |
| `Bookstore.Common` | SDK-style, `netstandard2.0` | SDK-style, `net10.0;netstandard2.0` | Multi-targeted constants-only library |
| `Bookstore.Cdk` | SDK-style, `net10.0` | Unchanged | Already on net10.0 |

### Core Migrations

1. **Entity Framework 6 → EF Core 10**
   - `ApplicationDbContext`: replaced `DbModelBuilder` with `ModelBuilder`, fluent API updated to EF Core syntax
   - `.HasRequired().WillCascadeOnDelete(false)` → `.HasOne().OnDelete(DeleteBehavior.Restrict)`
   - All repositories: `System.Data.Entity` → `Microsoft.EntityFrameworkCore`
   - `PaginatedList<T>`: now uses EF Core `CountAsync`/`ToListAsync` from `Microsoft.EntityFrameworkCore`
   - `Include(x => x.OrderItems.Select(y => y.Book))` → `Include(x => x.OrderItems).ThenInclude(y => y.Book)`
   - Removed `DropCreateDatabaseIfModelChanges` — seed data moved to `OnModelCreating` via `HasData()`
   - Startup calls `EnsureCreated()` to create database on first run

2. **ASP.NET MVC 5 → ASP.NET Core MVC**
   - All controllers: `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc`
   - `ActionResult` → `IActionResult`
   - All view models: `System.Web.Mvc.SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`
   - `HttpPostedFileBase` → `IFormFile` in upload view models and attributes

3. **OWIN → ASP.NET Core Middleware**
   - `Startup.cs` (OWIN) replaced by `Program.cs` (ASP.NET Core minimal hosting)
   - `Global.asax` replaced by `Program.cs`
   - `LocalAuthenticationMiddleware`: `OwinMiddleware` → `IMiddleware`
   - `AuthenticationConfig` (OWIN OpenIdConnect) → ASP.NET Core `AddOpenIdConnect()`

4. **DI: Autofac → Microsoft.Extensions.DependencyInjection**
   - `Autofac.Mvc5`, `Autofac.Owin` removed
   - All services registered with `builder.Services.Add*()`
   - `InstancePerRequest()` → `AddScoped()`

5. **Logging: NLog directly → NLog + `NLog.Extensions.Logging`**
   - NLog configured with `LoggingConfiguration` as before
   - Integrated with ASP.NET Core via `builder.Logging.AddNLog()`

6. **Configuration: `Web.config` + `ConfigurationManager` → `appsettings.json` + `IConfiguration`**
   - `appsettings.json` created with all settings from `Web.config`
   - `BookstoreConfiguration` now initialises from `IConfiguration` via a new `Initialize(IConfiguration)` static method
   - Backward-compatible `GetSetting`/`AddSetting` API preserved
   - AWS SSM Parameter Store loading moved to Program.cs startup

7. **Routing**
   - `AreaRegistration.RegisterAllAreas()` removed; `[Area("Admin")]` on `AdminAreaControllerBase`
   - Route registered in Program.cs via `MapControllerRoute`
   - `RouteConfig.cs` / `FilterConfig.cs` / `BundleConfig.cs` — emptied (logic moved to Program.cs)

8. **Bundling**
   - `System.Web.Optimization` (BundleConfig) removed
   - Views already reference CDN-hosted Bootstrap/jQuery directly — no bundling changes needed

9. **Static Files**
   - `UseStaticFiles()` in Program.cs serves from `wwwroot/`
   - Image cover paths updated from `/Content/Images/coverimages/` to `/images/coverimages/`

10. **Views**
    - `@Html.Partial("X")` → `<partial name="X" />` (resolves MVC1000 deadlock warnings)
    - `@Html.EnumDropDownListFor(...)` → `@Html.DropDownListFor(..., Html.GetEnumSelectList<T>(), ...)`
    - `_ViewImports.cshtml` updated to use `Bookstore.Web.ViewModel` namespaces and `Microsoft.AspNetCore.Mvc.Rendering`
    - Admin area `_ViewImports.cshtml` created

11. **AssemblyInfo.cs** — removed duplicate attributes from legacy files (SDK auto-generates these)

## Remaining Warnings (Non-Blocking)

### NU1901/NU1902/NU1903 — Magick.NET vulnerability advisories (~660 warnings)
- **Cause**: `Magick.NET-Q8-AnyCPU 14.6.0` is the latest available version and it bundles ImageMagick native binaries with ~100 known CVEs tracked by the NuGet advisory database. These are CVEs in the underlying C library (libMagick), not in .NET code.
- **Resolution options**: 
  - Replace Magick.NET with `SixLabors.ImageSharp` (pure .NET, no native deps, no CVEs) — see `21-system-drawing-migration.md`
  - Or accept the risk if the environment's OS is patched and images come from trusted sources only

### NU1510 — Removed
- `Microsoft.AspNetCore.Authentication.Cookies` explicit reference removed (auto-provided by the shared framework)

## Next Steps

1. **Replace Magick.NET with ImageSharp** to eliminate the ~660 NU19xx vulnerability warnings. The `ImageResizeService` uses `MagickImage` for resizing — `SixLabors.ImageSharp` provides a direct equivalent.

2. **Database migrations**: The current setup uses `EnsureCreated()`. For production deployments, generate proper EF Core migrations:
   ```bash
   cd app/Bookstore.Data
   dotnet ef migrations add InitialCreate --startup-project ../Bookstore.Web
   dotnet ef database update --startup-project ../Bookstore.Web
   ```

3. **Dockerfile**: The existing `Dockerfile` targets Windows containers. Update base images to Linux:
   - `mcr.microsoft.com/dotnet/sdk:10.0` for build
   - `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime
   See `19-linux-containerization.md`.

4. **Cognito/OIDC callback URL**: The ASP.NET Core OIDC callback path is `/signin-oidc` by default. Update the Cognito App Client's allowed callback URLs accordingly.

5. **Amazon.CDK.Lib 2.188.0** has a low-severity advisory (NU1901) — upgrade CDK to latest when convenient.
