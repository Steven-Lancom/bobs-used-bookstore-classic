# Assessment Report: BobsBookstoreClassic

## Solution Overview

| Attribute | Value |
|-----------|-------|
| **Solution Name** | BobsBookstoreClassic |
| **Total Projects** | 5 |
| **Target Framework** | net10.0 |
| **Total Lines of Code** | 8592 |
| **Overall Complexity** | Critical |
| **Total NuGet Packages** | 62 (across all projects) |
| **Incompatible Packages** | 12 |
| **.NET Core Readiness** | Not Ready |
| **Linux Readiness** | Not Ready |

## Executive Summary

**Solution Migration Mode: COMPLEX**

BobsBookstoreClassic is a layered ASP.NET MVC 5 e-commerce application following a classic Web → Data → Domain architecture, plus a shared Common library and an AWS CDK infrastructure project. The web tier (Bookstore.Web) runs on .NET Framework 4.8 with OWIN middleware for OpenID Connect authentication, Autofac for dependency injection, Entity Framework 6 for data access, and NLog with AWS CloudWatch for logging. The solution also includes a CDK project on net6.0 for AWS infrastructure provisioning.

Three of the five projects target .NET Framework 4.8 with old-style .csproj format and require full conversion to SDK-style projects targeting net10.0. The CDK project is already SDK-style but needs a TFM bump from net6.0. Bookstore.Common targets netstandard2.0 and requires no migration.

- **1 Low-complexity project** (Bookstore.Common) — already netstandard2.0, no migration needed
- **1 Low-complexity project** (Bookstore.Domain) — pure domain model library, zero NuGet packages, just needs project format conversion
- **1 Low-complexity project** (Bookstore.Cdk) — already SDK-style, only needs TFM bump and package upgrades
- **1 Medium-complexity project** (Bookstore.Data) — EF6 → EF Core migration, AWS SDK upgrades, project format conversion
- **1 Critical-complexity project** (Bookstore.Web) — MVC 5 → ASP.NET Core MVC, OWIN → Core middleware, Autofac.Mvc5 → Core DI, OpenID Connect auth rewrite, 54 packages (12 incompatible), 42 Razor views, 16 controllers, Global.asax/Startup.cs rewrite

The primary transformation challenge is the Bookstore.Web project, which requires a complete rewrite of the application bootstrapping pipeline: OWIN middleware must be replaced with ASP.NET Core middleware, Autofac.Mvc5 must be replaced with ASP.NET Core's built-in DI (or Autofac.Extensions.DependencyInjection), OpenID Connect authentication must migrate from Microsoft.Owin.Security.OpenIdConnect to Microsoft.AspNetCore.Authentication.OpenIdConnect, and Entity Framework 6 must be replaced with EF Core throughout the data layer.

### Key Statistics

| Metric | Count |
|--------|-------|
| Projects requiring format conversion | 3 (legacy to SDK-style) |
| Blocking issues | 0 |
| Razor views to migrate | 42 |
| Controllers to migrate | 16 |
| Total estimated changes | 103 |

## Project Analysis Table

| Project | Current Framework | Target | LOC | Packages | Incompatible | Complexity |
|---------|-------------------|--------|-----|----------|--------------|------------|
| Bookstore.Common | netstandard2.0 | netstandard2.0 | 6 | 0 | 0 | Low |
| Bookstore.Domain | net4.8 | net10.0 | 1813 | 0 | 0 | Low |
| Bookstore.Data | net4.8 | net10.0 | 1042 | 4 | 0 | Medium |
| Bookstore.Cdk | net6.0 | net10.0 | 595 | 4 | 0 | Low |
| Bookstore.Web | net4.8 | net10.0 | 5136 | 54 | 12 | Critical |

## Cross-Project Package Summary

| Package | Used By | Version(s) | Compatible | Notes |
|---------|---------|------------|------------|-------|
| EntityFramework | Bookstore.Data, Bookstore.Web | 6.5.1 | Yes* | Targets netstandard2.1; resolves on net10.0 but should be replaced with Microsoft.EntityFrameworkCore |
| AWSSDK.S3 | Bookstore.Data, Bookstore.Web | 3.7.416.5 | Yes | UpgradePackage — latest 4.0.104.1 |
| AWSSDK.Rekognition | Bookstore.Data, Bookstore.Web | 3.7.400.129 | Yes | UpgradePackage — latest 4.0.101.1 |
| AWSSDK.Core | Bookstore.Web | 3.7.402.35 | Yes | UpgradePackage — latest 4.0.102.8 (transitive for Data) |
| Microsoft.AspNet.Mvc | Bookstore.Web | 5.3.0 | No | ReplacePackage — replace with ASP.NET Core MVC (Microsoft.NET.Sdk.Web) |
| Microsoft.Owin | Bookstore.Web | 4.2.2 | No | ReplacePackage — remove, use ASP.NET Core middleware |
| Microsoft.Owin.Security.OpenIdConnect | Bookstore.Web | 4.2.2 | No | ReplacePackage — replace with Microsoft.AspNetCore.Authentication.OpenIdConnect |
| Autofac.Mvc5 | Bookstore.Web | 6.1.0 | No | ReplacePackage — replace with Autofac.Extensions.DependencyInjection or built-in DI |
| Autofac | Bookstore.Web | 8.2.1 | Yes | UpgradePackage — latest 9.3.4; targets netstandard2.0 |
| NLog | Bookstore.Web | 5.4.0 | Yes | UpgradePackage — latest 6.2.1 |
| Newtonsoft.Json | Bookstore.Web | 13.0.3 | Yes | KeepPackage — current stable, targets netstandard2.0 |

## Cross-Project Dependencies

Bookstore.Web (Critical)
  - Bookstore.Common (Low)
  - Bookstore.Data (Medium)
    - Bookstore.Domain (Low)
  - Bookstore.Domain (Low)

Bookstore.Cdk (Low)
  - Bookstore.Common (Low)

### Recommended Transformation Order (Dependency-First)

1. **Bookstore.Common** — No migration needed (netstandard2.0, already cross-platform compatible)
2. **Bookstore.Domain** — Leaf library, zero dependencies, zero packages; convert to SDK-style net10.0
3. **Bookstore.Data** — Depends on Domain; EF6 → EF Core, project format conversion, AWS SDK upgrades
4. **Bookstore.Cdk** — Depends on Common; already SDK-style, TFM bump net6.0 → net10.0, package upgrades
5. **Bookstore.Web** — Top-level web project, depends on Common + Data + Domain; migrate last after all dependencies are on net10.0

## Key Findings

1. **MVC 5 to ASP.NET Core MVC migration**: Bookstore.Web is an ASP.NET MVC 5 application with 16 controllers and 42 Razor views that must be migrated to ASP.NET Core MVC. All System.Web.Mvc references, route configuration (RouteConfig.cs), filter configuration (FilterConfig.cs), and bundle configuration (BundleConfig.cs) must be rewritten.

2. **OWIN middleware pipeline requires complete replacement**: The application uses Microsoft.Owin with OpenID Connect authentication (Startup.cs), cookie authentication, and a custom LocalAuthenticationMiddleware. This entire OWIN pipeline must be replaced with ASP.NET Core middleware and authentication configuration in Program.cs.

3. **Autofac DI container migration**: Bookstore.Web uses Autofac.Mvc5 (DependencyInjectionSetup.cs) for dependency injection. This must be replaced with ASP.NET Core's built-in DI container or Autofac.Extensions.DependencyInjection. The Autofac container registrations in DependencyInjectionSetup.cs must be ported.

4. **Entity Framework 6 to EF Core migration**: Both Bookstore.Data and Bookstore.Web reference EntityFramework 6.5.1. The ApplicationDbContext, BookstoreDbInitializer, BookstoreConfiguration, and all repository classes must be migrated to EF Core patterns (IEntityTypeConfiguration, DbContext options via DI, migration-based initialization).

5. **Three projects require legacy-to-SDK project format conversion**: Bookstore.Domain, Bookstore.Data, and Bookstore.Web use old-style .csproj with explicit Compile includes, assembly references, and Properties/AssemblyInfo.cs files that must be converted to SDK-style format.

6. **AWS SDK v3 to v4 upgrade opportunity**: All AWSSDK packages (S3, Rekognition, CloudWatchLogs, SSM, Core) are on v3.7.x; the latest major version is v4.0.x which targets net8.0+ natively. This is an upgrade, not a blocker.

7. **Admin Area with area registration**: Bookstore.Web has an Admin area with its own AreaRegistration class, controllers, and views. The area registration pattern must be converted to ASP.NET Core area routing conventions.

## External Dependencies

| Dependency | Type | Impact |
|------------|------|--------|
| AWS S3 | Cloud Service | Used for file storage (S3FileService.cs); AWSSDK.S3 upgrade to v4 required |
| AWS Rekognition | Cloud Service | Used for image validation (RekognitionImageValidationService.cs); AWSSDK.Rekognition upgrade to v4 required |
| AWS CloudWatch Logs | Cloud Service | Used for logging via NLog (AWS.Logger.NLog); upgrade to v5.0.1 required |
| AWS Systems Manager | Cloud Service | Used for configuration (ConfigurationSetup.cs); AWSSDK.SimpleSystemsManagement upgrade to v4 required |
| AWS CDK | Cloud Service | Infrastructure provisioning (Bookstore.Cdk); Amazon.CDK.Lib upgrade to latest required |
| OpenID Connect Provider | Identity | Authentication via OWIN OpenIdConnect; must migrate to ASP.NET Core OpenIdConnect middleware |
| SQL Server | Database | Entity Framework 6 DbContext (ApplicationDbContext); must migrate to EF Core with Microsoft.Data.SqlClient |

## Actionable Next Steps

1. **Phase 1 — Leaf libraries** (Low risk): Convert Bookstore.Domain to SDK-style net10.0 .csproj. Bookstore.Common requires no changes (netstandard2.0).

2. **Phase 2 — Data layer** (Medium risk): Convert Bookstore.Data to SDK-style net10.0, migrate EntityFramework 6 to Microsoft.EntityFrameworkCore, upgrade AWSSDK packages, upgrade Magick.NET, and migrate ApplicationDbContext/repositories to EF Core patterns.

3. **Phase 3 — CDK project** (Low risk): Bump Bookstore.Cdk TargetFramework from net6.0 to net10.0, upgrade Amazon.CDK.Lib, Cdklabs.CdkNag, and Constructs to latest versions.

4. **Phase 4 — Web application** (High risk): Convert Bookstore.Web to ASP.NET Core MVC targeting net10.0. Replace OWIN pipeline with ASP.NET Core middleware, migrate OpenID Connect authentication, replace Autofac.Mvc5 with built-in DI or Autofac.Extensions.DependencyInjection, remove Global.asax, create Program.cs, migrate all 42 Razor views and 16 controllers to ASP.NET Core conventions, move static files to wwwroot, and replace BundleConfig with modern bundling.

5. **Phase 5 — Integration validation** (Medium risk): Full solution build verification, authentication flow testing, EF Core data access verification, and AWS service integration testing.

---

## Per-Project Assessment Details

### Bookstore.Common

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | netstandard2.0 |
| **Lines of Code** | 6 |
| **NuGet Packages** | 0 |
| **Project References** | 0 |
| **Complexity** | Low |
| **Estimated Changes** | 0 |

#### Migration Analysis

##### Migration Strategy

1. **No migration needed** — Bookstore.Common targets netstandard2.0, which is fully compatible with net10.0. The project is already SDK-style format and contains only a single Constants.cs file with shared constants.
2. Verify the project continues to build and is consumable by all dependent projects (Bookstore.Cdk and Bookstore.Web) after they are migrated to net10.0.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| Minimal risk | Low | Zero dependencies, zero packages, single file with constants. No API surface changes needed. |

##### Recommendations

1. Leave Bookstore.Common on netstandard2.0 as-is — it is already cross-platform compatible and consumable by both .NET Framework and modern .NET projects.
2. If desired in the future, the target could be changed to net10.0 for access to newer APIs, but this is not required.

##### Cross-Project Impact

Bookstore.Common is referenced by Bookstore.Cdk and Bookstore.Web. Because it remains on netstandard2.0 (unchanged), it creates no migration dependency — dependent projects can be migrated independently.

---

### Bookstore.Domain

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 1813 |
| **NuGet Packages** | 0 |
| **Project References** | 0 |
| **Complexity** | Low |
| **Estimated Changes** | 2 |

#### Migration Analysis

##### Migration Strategy

1. **Convert old-style .csproj to SDK-style** — Replace the verbose MSBuild project file with a minimal SDK-style `<Project Sdk="Microsoft.NET.Sdk">` format. Remove all explicit `<Compile Include="...">` items (SDK-style auto-includes *.cs). Set `<TargetFramework>net10.0</TargetFramework>`. Preserve `<RootNamespace>Bookstore.Domain</RootNamespace>` and `<AssemblyName>Bookstore.Domain</AssemblyName>`.
2. **Remove Properties/AssemblyInfo.cs** — Enable `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>` (default in SDK-style) and delete the hand-written AssemblyInfo.cs, or transfer any custom attributes to the .csproj properties.
3. **Verify all domain model classes compile** — The project contains pure C# domain entities (Book, Order, Customer, Offer, Address, ShoppingCart), DTOs, service classes, repository interfaces, and extension methods. None reference System.Web or framework-specific APIs, so no code changes are expected.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| System.ComponentModel.DataAnnotations reference | Low | This assembly reference is available in net10.0 as a built-in package; no action required beyond removing the explicit assembly reference during SDK-style conversion. |

##### Recommendations

1. Migrate Bookstore.Domain first (or in parallel with Bookstore.Common) since it is a leaf library with zero NuGet dependencies and zero project references.
2. After conversion, confirm that all repository interfaces and domain service classes compile cleanly to validate the net10.0 target before proceeding to Bookstore.Data.

##### Cross-Project Impact

Bookstore.Domain is referenced by Bookstore.Data and Bookstore.Web. It must be migrated before Bookstore.Data. Since it has no external dependencies, its migration is low-risk and unblocks the data layer migration.

---

### Bookstore.Data

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 1042 |
| **NuGet Packages** | 4 |
| **Project References** | 1 |
| **Complexity** | Medium |
| **Estimated Changes** | 7 |

#### Package Compatibility (4 packages, 0 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| AWSSDK.Rekognition | 3.7.400.129 | COMPATIBLE | UpgradePackage |
| AWSSDK.S3 | 3.7.416.5 | COMPATIBLE | UpgradePackage |
| EntityFramework | 6.5.1 | COMPATIBLE | ReplacePackage |
| Magick.NET-Q8-AnyCPU | 14.6.0 | COMPATIBLE | UpgradePackage |

#### Project Dependencies (1)

- Bookstore.Domain

#### Legacy Files Inventory (1 files across 1 kinds)

| Kind | Files |
|------|------:|
| `.config` | 1 |

#### Migration Analysis

##### Migration Strategy

1. **Convert old-style .csproj to SDK-style** — Replace with `<Project Sdk="Microsoft.NET.Sdk">`, set `<TargetFramework>net10.0</TargetFramework>`, convert all assembly `<Reference>` entries to `<PackageReference>` entries, remove explicit `<Compile>` items. Preserve RootNamespace and AssemblyName.
2. **Remove Properties/AssemblyInfo.cs** — Enable auto-generated assembly info via SDK defaults.
3. **Replace EntityFramework 6.5.1 with Microsoft.EntityFrameworkCore** — Migrate ApplicationDbContext from `System.Data.Entity.DbContext` to `Microsoft.EntityFrameworkCore.DbContext`. Replace `DbModelBuilder` configuration (BookstoreConfiguration.cs) with `IEntityTypeConfiguration<T>` implementations. Replace `Database.SetInitializer` (BookstoreDbInitializer.cs) with EF Core migrations or `EnsureCreated`. Update all repository classes to use EF Core query patterns (e.g. `Include`/`ThenInclude` instead of string-based includes).
4. **Upgrade AWSSDK.Rekognition** from 3.7.400.129 to latest v4 (4.0.101.1) and **AWSSDK.S3** from 3.7.416.5 to v4 (4.0.104.1). Review breaking API changes in the v3→v4 migration (namespace and client construction changes).
5. **Upgrade Magick.NET-Q8-AnyCPU** from 14.6.0 to 14.17.2 (latest, targets net8.0+).
6. **Remove App.config** — Connection string and EF configuration will move to the host project's appsettings.json and DI-based DbContext options.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| EF6 → EF Core migration | Medium | ApplicationDbContext, BookstoreDbInitializer, BookstoreConfiguration, and 7 repository classes must be updated. EF Core has different lazy loading, relationship configuration, and initialization patterns. |
| AWS SDK v3 → v4 breaking changes | Medium | AWSSDK v4 changes client constructors, credential resolution, and some API shapes. S3FileService.cs and RekognitionImageValidationService.cs must be updated. |
| System.Configuration dependency | Low | BookstoreConfiguration.cs may use ConfigurationManager; must be replaced with IConfiguration/IOptions injection. |

##### Recommendations

1. Migrate after Bookstore.Domain is on net10.0 to ensure the ProjectReference resolves correctly.
2. Tackle the EF6 → EF Core migration methodically: first convert the DbContext and entity configurations, then update each repository one at a time, testing queries as you go.
3. Consider keeping AWSSDK on v3.7.x initially (they target netstandard2.0 and are compatible) and upgrading to v4 as a separate step after the framework migration succeeds.

##### Cross-Project Impact

Bookstore.Data is referenced by Bookstore.Web. It depends on Bookstore.Domain, which must be migrated first. The EF Core migration in this project directly affects Bookstore.Web's DbContext registration and startup configuration.

---

### Bookstore.Cdk

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net6.0 |
| **Lines of Code** | 595 |
| **NuGet Packages** | 4 |
| **Project References** | 1 |
| **Complexity** | Low |
| **Estimated Changes** | 4 |

#### Package Compatibility (4 packages, 0 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| Amazon.CDK.Lib | 2.188.0 | COMPATIBLE | UpgradePackage |
| Cdklabs.CdkNag | 2.35.66 | COMPATIBLE | UpgradePackage |
| Constructs | 10.4.2 | COMPATIBLE | UpgradePackage |
| Amazon.Jsii.Analyzers | * | COMPATIBLE | KeepPackage |

#### Project Dependencies (1)

- Bookstore.Common

#### Migration Analysis

##### Migration Strategy

1. **Update TargetFramework from net6.0 to net10.0** — Change `<TargetFramework>net6.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>` in the .csproj. The `<RollForward>Major</RollForward>` setting can be removed once targeting net10.0 directly.
2. **Upgrade Amazon.CDK.Lib** from 2.188.0 to latest (2.272.0). Review CDK construct changes between these versions for any breaking changes in CoreStack.cs, DatabaseStack.cs, EcsStack.cs, and NetworkStack.cs.
3. **Upgrade Cdklabs.CdkNag** from 2.35.66 to latest (3.0.2). Note: this is a major version bump — check for breaking API changes.
4. **Upgrade Constructs** from 10.4.2 to latest (10.8.1).

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| Cdklabs.CdkNag 2.x → 3.x major version bump | Low | May have breaking API changes; review GlobalSuppressions.cs and any NagPack configurations. |
| CDK construct deprecations | Low | Between CDK 2.188.0 and 2.272.0 some constructs may have been deprecated or had property changes. |

##### Recommendations

1. The Bookstore.Cdk project is already SDK-style and only needs a TFM bump, making it a straightforward migration. Upgrade it after Bookstore.Common (its only dependency) is confirmed stable — though Common requires no changes.
2. Run `cdk synth` after upgrading to verify all stacks synthesize correctly.

##### Cross-Project Impact

Bookstore.Cdk depends only on Bookstore.Common (which requires no migration). It is not referenced by any other project, so it can be migrated independently and in parallel with the main application projects.

---

### Bookstore.Web

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 5136 |
| **NuGet Packages** | 54 |
| **Project References** | 3 |
| **Complexity** | Critical |
| **Estimated Changes** | 90 |

#### Package Compatibility (54 packages, 12 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| Antlr | 3.5.0.2 | COMPATIBLE | ReplacePackage |
| Autofac | 8.2.1 | COMPATIBLE | UpgradePackage |
| Autofac.Mvc5 | 6.1.0 | INCOMPATIBLE | ReplacePackage |
| Autofac.Owin | 7.1.0 | INCOMPATIBLE | ReplacePackage |
| AWS.Logger.Core | 3.3.3 | COMPATIBLE | UpgradePackage |
| AWS.Logger.NLog | 3.3.4 | COMPATIBLE | UpgradePackage |
| AWSSDK.CloudWatchLogs | 3.7.410.17 | COMPATIBLE | UpgradePackage |
| AWSSDK.Core | 3.7.402.35 | COMPATIBLE | UpgradePackage |
| AWSSDK.Rekognition | 3.7.400.129 | COMPATIBLE | UpgradePackage |
| AWSSDK.S3 | 3.7.416.5 | COMPATIBLE | UpgradePackage |
| AWSSDK.SimpleSystemsManagement | 3.7.404.10 | COMPATIBLE | UpgradePackage |
| EntityFramework | 6.5.1 | COMPATIBLE | ReplacePackage |
| jQuery | 3.7.1 | COMPATIBLE | ReplacePackage |
| jQuery.Validation | 1.21.0 | COMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Mvc | 5.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Razor | 3.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Web.Optimization | 1.1.3 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.WebPages | 3.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Bcl.AsyncInterfaces | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Bcl.Memory | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Bcl.TimeProvider | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform | 4.1.0 | COMPATIBLE | ReplacePackage |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Extensions.Logging.Abstractions | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.IdentityModel.Abstractions | 8.7.0 | COMPATIBLE | KeepPackage |
| Microsoft.IdentityModel.JsonWebTokens | 8.7.0 | COMPATIBLE | KeepPackage |
| Microsoft.IdentityModel.Logging | 8.7.0 | COMPATIBLE | KeepPackage |
| Microsoft.IdentityModel.Protocols | 8.7.0 | COMPATIBLE | KeepPackage |
| Microsoft.IdentityModel.Protocols.OpenIdConnect | 8.7.0 | COMPATIBLE | KeepPackage |
| Microsoft.IdentityModel.Tokens | 8.7.0 | COMPATIBLE | KeepPackage |
| Microsoft.jQuery.Unobtrusive.Validation | 4.0.0 | COMPATIBLE | ReplacePackage |
| Microsoft.Owin | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Host.SystemWeb | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security.Cookies | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security.OpenIdConnect | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Web.Infrastructure | 2.0.1 | COMPATIBLE | ReplacePackage |
| Modernizr | 2.8.3 | COMPATIBLE | ReplacePackage |
| Newtonsoft.Json | 13.0.3 | COMPATIBLE | KeepPackage |
| NLog | 5.4.0 | COMPATIBLE | UpgradePackage |
| Owin | 1.0 | INCOMPATIBLE | ReplacePackage |
| System.Buffers | 4.6.1 | COMPATIBLE | ReplacePackage |
| System.Diagnostics.DiagnosticSource | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.IdentityModel.Tokens.Jwt | 8.7.0 | COMPATIBLE | KeepPackage |
| System.IO.Pipelines | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Memory | 4.6.3 | COMPATIBLE | ReplacePackage |
| System.Numerics.Vectors | 4.6.1 | COMPATIBLE | ReplacePackage |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | COMPATIBLE | ReplacePackage |
| System.Text.Encoding | 4.3.0 | COMPATIBLE | ReplacePackage |
| System.Text.Encodings.Web | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Text.Json | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Threading.Tasks.Extensions | 4.6.3 | COMPATIBLE | ReplacePackage |
| System.ValueTuple | 4.6.1 | COMPATIBLE | ReplacePackage |
| WebGrease | 1.6.0 | COMPATIBLE | ReplacePackage |

#### Project Dependencies (3)

- Bookstore.Common
- Bookstore.Data
- Bookstore.Domain

#### Legacy Files Inventory (49 files across 3 kinds)

| Kind | Files |
|------|------:|
| `.asax` | 1 |
| `.cshtml` | 42 |
| `.config` | 6 |

#### Migration Analysis

##### Migration Strategy

1. **Convert old-style .csproj to SDK-style** — Replace the verbose .csproj with `<Project Sdk="Microsoft.NET.Sdk.Web">`, set `<TargetFramework>net10.0</TargetFramework>`. Remove all explicit `<Compile>`, `<Content>`, and assembly `<Reference>` items. Convert packages.config entries to `<PackageReference>`. Preserve RootNamespace and AssemblyName.
2. **Remove Properties/AssemblyInfo.cs** — Enable auto-generated assembly info.
3. **Replace Global.asax with Program.cs** — Delete Global.asax and Global.asax.cs. Create a new Program.cs with `WebApplication.CreateBuilder()` that composes service registration and middleware pipeline. Port Application_Start logic (BundleConfig, FilterConfig, RouteConfig, AuthenticationSetup, ConfigurationSetup, DependencyInjectionSetup, LoggingSetup) into Program.cs builder/app configuration.
4. **Replace OWIN middleware with ASP.NET Core middleware** — Remove Startup.cs (OWIN), Microsoft.Owin.*, and Owin packages. Rewrite authentication setup: replace `app.UseCookieAuthentication()` and `app.UseOpenIdConnectAuthentication()` with `builder.Services.AddAuthentication().AddCookie().AddOpenIdConnect()` in Program.cs. Port the custom LocalAuthenticationMiddleware to ASP.NET Core `IMiddleware`.
5. **Replace Autofac.Mvc5 with ASP.NET Core DI** — Remove Autofac.Mvc5 and Autofac.Owin packages. Port DependencyInjectionSetup.cs registrations to `builder.Services.AddScoped/AddTransient/AddSingleton()` calls, or use Autofac.Extensions.DependencyInjection with `builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())`.
6. **Replace MVC 5 packages** — Remove Microsoft.AspNet.Mvc, Microsoft.AspNet.Razor, Microsoft.AspNet.WebPages, Microsoft.AspNet.Web.Optimization. The SDK.Web provides ASP.NET Core MVC. Update all controllers from `System.Web.Mvc.Controller` to `Microsoft.AspNetCore.Mvc.Controller`. Replace `System.Web.Mvc` action results, model binding, and filter attributes with their ASP.NET Core equivalents.
7. **Migrate 16 controllers** — Update using statements from `System.Web.Mvc` to `Microsoft.AspNetCore.Mvc`. Replace `HttpContext.Current` with injected `IHttpContextAccessor`. Replace `Server.MapPath` with `IWebHostEnvironment.ContentRootPath`. Update `HttpPostedFileBase` to `IFormFile`. Port Area registration (AdminAreaRegistration.cs) to ASP.NET Core area conventions.
8. **Migrate 42 Razor views** — Replace `@Scripts.Render`/`@Styles.Render` with direct `<script>`/`<link>` tags. Replace `@Html.ActionLink`/`@Url.Action` with Tag Helpers (`<a asp-controller="" asp-action="">`). Update `_ViewImports.cshtml` to import ASP.NET Core Tag Helpers. Migrate `_Layout.cshtml` for both main and Admin areas.
9. **Migrate Web.config to appsettings.json** — Extract `<appSettings>` and `<connectionStrings>` from Web.config into appsettings.json. Remove Web.config, Web.Debug.config, Web.Release.config, Views/Web.config, Areas/Admin/Views/web.config, and packages.config. Replace `ConfigurationManager` calls with `IConfiguration` injection.
10. **Move static files to wwwroot** — Move Content/ and Scripts/ directories into wwwroot/. Add `app.UseStaticFiles()` to Program.cs. Remove jQuery, jQuery.Validation, Modernizr, Microsoft.jQuery.Unobtrusive.Validation NuGet packages and deliver these client-side assets via wwwroot/LibMan or CDN.
11. **Remove polyfill/inbox packages** — Remove System.Buffers, System.Memory, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe, System.Text.Encoding, System.Text.Encodings.Web, System.Text.Json, System.IO.Pipelines, System.Threading.Tasks.Extensions, System.ValueTuple, System.Diagnostics.DiagnosticSource, Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.Memory, Microsoft.Bcl.TimeProvider, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions — these are all inbox on net10.0.
12. **Remove build-tooling packages** — Remove Antlr, WebGrease, Microsoft.CodeDom.Providers.DotNetCompilerPlatform, Microsoft.Web.Infrastructure — not needed in SDK-style projects.
13. **Replace EntityFramework 6.5.1** — Same as Bookstore.Data: replace with Microsoft.EntityFrameworkCore and update DbContext registration to use `builder.Services.AddDbContext<ApplicationDbContext>()`.
14. **Upgrade remaining packages** — Upgrade AWSSDK.* to v4, AWS.Logger.Core to 4.0.3, AWS.Logger.NLog to 5.0.1, Autofac to 9.3.4 (if retained), NLog to 6.2.1.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| OWIN → ASP.NET Core middleware rewrite | High | The entire authentication pipeline (OpenID Connect, cookie auth, custom LocalAuthenticationMiddleware) must be rewritten. Incorrect migration could break authentication flows entirely. |
| Autofac.Mvc5 → Core DI | High | All DI registrations in DependencyInjectionSetup.cs must be ported. Missing registrations will cause runtime failures. |
| MVC 5 → ASP.NET Core MVC | High | 16 controllers and 42 views must update namespaces, action result types, model binding, and routing. System.Web.Mvc types have no direct drop-in equivalents. |
| Admin Area migration | Medium | AdminAreaRegistration.cs uses MVC 5 area registration; must convert to ASP.NET Core conventional area routing with `[Area("Admin")]` attributes and route templates. |
| Entity Framework 6 → EF Core | Medium | DbContext configuration, initialization, and query patterns differ. Must coordinate with Bookstore.Data migration. |
| HttpContext.Current usage | Medium | HttpContextExtensions.cs and IOwinRequestExtensions.cs use static HttpContext access; must be replaced with IHttpContextAccessor DI. |
| BundleConfig removal | Low | @Scripts.Render/@Styles.Render calls across all views must be replaced with direct script/link tags. |
| Static file path changes | Low | Content/ and Scripts/ directories move to wwwroot/; all path references in views must be updated. |

##### Recommendations

1. Migrate Bookstore.Web last, after Bookstore.Domain, Bookstore.Data, and Bookstore.Common are all verified on their target frameworks.
2. Start with the project format conversion and package cleanup (steps 1-2, 11-12), then tackle the middleware/auth pipeline (steps 3-4), then DI (step 5), then controllers and views (steps 6-8).
3. Keep the OWIN-to-Core authentication migration as a focused, isolated step — test authentication end-to-end before proceeding to controller migration.
4. Consider retaining Autofac via Autofac.Extensions.DependencyInjection rather than rewriting all registrations to built-in DI, to reduce migration risk.
5. Use bulk namespace replacement (sed/ast-grep) for the System.Web.Mvc → Microsoft.AspNetCore.Mvc controller updates across all 16 controllers.

##### Cross-Project Impact

Bookstore.Web is the top-level application project. It depends on Bookstore.Common (no changes needed), Bookstore.Domain (must be migrated first), and Bookstore.Data (must be migrated first, including EF Core migration). The EF Core DbContext registration in Bookstore.Web's Program.cs must align with the migrated ApplicationDbContext in Bookstore.Data.
