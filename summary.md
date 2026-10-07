# Migration Summary: BobsBookstoreClassic .NET Framework → .NET 10

## Status
**COMPLETE** — `dotnet build BobsBookstoreClassic.sln` exits with code 0, **0 errors, 0 warnings**.

## Starting Point
AWS Transform Custom CLI (atx) had already migrated all five projects to SDK-style project files targeting `net10.0`. The solution compiled successfully at handoff with 652 warnings and 0 errors.

## Changes Made in This Cycle

### 1. Magick.NET-Q8-AnyCPU upgrade (`Bookstore.Data.csproj`)
- **Before:** `14.6.0` — had 130+ known vulnerability advisories (NU190x warnings covering low/moderate/high severities)
- **After:** `14.17.2` — latest stable release, clears all NU190x vulnerability warnings

### 2. Microsoft.Extensions.Configuration.Abstractions version pin (`Bookstore.Data.csproj`)
- **Before:** `10.0.0-preview.4.25258.19` — version no longer published on NuGet, causing NU1603 resolution warning
- **After:** `10.0.0-preview.4.25258.110` — the version that was already being resolved; pinning it eliminates the warning

### 3. Amazon.CDK.Lib + Constructs upgrade (`Bookstore.Cdk.csproj`)
- **Before:** `Amazon.CDK.Lib 2.188.0` (known low severity vulnerability NU1901) + `Constructs 10.4.2`
- **After:** `Amazon.CDK.Lib 2.272.0` + `Constructs 10.5.0` — Constructs bumped to satisfy CDK.Lib's minimum dependency, clears the NU1901 warning

### 4. CloudFront CDK construct migration (`CoreStack.cs`)
- **Before:** Used deprecated `CloudFrontWebDistribution` + `CloudFrontWebDistributionProps` (CS0618/CS0612 obsolete warnings introduced when CDK.Lib was bumped to 2.272.0)
- **After:** Migrated to the current `Distribution` + `S3BucketOrigin.WithOriginAccessIdentity()` API, eliminating all CS0612/CS0618 warnings
- Added `using Amazon.CDK.AWS.CloudFront.Origins;` (bundled in `Amazon.CDK.Lib`)
- Behavioral equivalence maintained: same S3 bucket origin, same OAI, HTTPS redirect, GET/HEAD/OPTIONS allowed methods, and compression enabled

## Final Build Result
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

All five projects build cleanly targeting `net10.0`:
- `Bookstore.Common` (netstandard2.0 — unchanged, compatible)
- `Bookstore.Domain` (net10.0)
- `Bookstore.Data` (net10.0)
- `Bookstore.Web` (net10.0)
- `Bookstore.Cdk` (net10.0)

## Next Steps
None required for build integrity. Optional future work:
- `Bookstore.Common` still targets `netstandard2.0`. It could be retargeted to `net10.0` if cross-framework sharing is no longer needed, which would allow use of net10.0-only APIs in that library.
- The CDK stack uses `OriginAccessIdentity` (OAI), which AWS recommends replacing with **Origin Access Control (OAC)** via `S3BucketOrigin.WithOriginAccessControl()` for new distributions. OAI continues to work but OAC is the preferred path for new deployments. This is a CDK infrastructure concern, not a compilation issue.
