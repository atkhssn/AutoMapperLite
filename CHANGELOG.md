# Changelog

All notable changes to AutoMapperLite are documented here.

## [4.0.2] — 2026-08-29 — Current official release

- **Fixed:** the test project's updated tooling (`xunit.v3`, `Microsoft.NET.Test.Sdk` 18.9.0) only supports net8.0+, and a Microsoft Testing Platform v2 change drops `dotnet test`'s legacy VSTest bridge on the .NET 10 SDK. `AutoMapperLite.Tests` now targets `net8.0;net9.0;net10.0` (the library itself is unaffected and still targets `net6.0;net7.0;net8.0;net9.0;net10.0`), and a repo-root `global.json` opts `dotnet test` into native MTP mode.
- **Fixed:** `Activator.CreateInstance` failures (e.g. mapping to a positional record with no parameterless constructor) now throw a clear `InvalidOperationException` naming the type, instead of a bare `MissingMethodException`.
- Added real, measured BenchmarkDotNet comparisons against manual mapping, AutoMapper 16.2.0, and Mapster 10.0.12 (see `docs/performance.html`). AutoMapperLite is **not** the fastest of the four — the docs and package description no longer implied otherwise.
- Verified and documented several previously-untested behaviors: a `null` source property is skipped (not assigned, so it won't overwrite a destination's own default), auto-map requires an exact type match (different enum types and numeric widening are not converted), and records with a parameterless constructor + init-only properties map correctly.
- All NuGet packages kept at their latest versions throughout (`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.11, `Microsoft.NET.Test.Sdk` 18.9.0, `xunit.v3` 4.0.0, `BenchmarkDotNet` 0.15.8) — none downgraded.

## [4.0.1] — 2026-08-29

Official rebrand and release. **All versions prior to 4.0.1 are deprecated** — upgrade.

- Official project website: [automapperlite.atkhssn.info](https://automapperlite.atkhssn.info)
- New visual identity: icon, favicons, app icons (Apple/Android/Windows), Open Graph / Twitter social preview images, light/dark logo lockups
- Full XML documentation comments on the public API (IntelliSense support in IDEs, `AutoMapperLite.xml` shipped in the package for every target framework)
- NuGet package metadata overhauled: accurate, non-hyperbolic description, `PackageProjectUrl`, refined tags for discoverability
- Static documentation site expanded with a Getting Started guide, a full usage guide, a performance page with real (not fabricated) benchmark numbers, a feature comparison, an FAQ, SEO/AEO metadata, and structured data
- Carries forward every fix and improvement from the 3.0.x line below — no public API breaking changes since 3.0.3

## [3.0.3] — 2026-08-29

- Now multi-targets `net6.0`/`net7.0`/`net8.0`/`net9.0`/`net10.0` (previously `net8.0` only)
- **Fixed:** mapping to a value-type (struct) destination silently produced an unpopulated struct, because properties were set on transient boxed copies instead of the real destination
- **Fixed:** a `List<T>` destination *property* was never auto-mapped even when a map was registered for the item types — only when one was registered for the exact `List<TSource>`/`List<TDestination>` pair, which nothing ever registers
- Hardened `MapperConfig` and the DI registration for concurrent use (`ConcurrentDictionary`, singleton `IMapper`)
- Reduced mapping-hot-path allocations and reflection overhead by caching resolved `PropertyInfo`/`MethodInfo` instead of re-resolving them on every call (~3.3–4x faster, ~6.5–8x less allocation per map, measured via the repo's BenchmarkDotNet suite)
- Clearer exception when a `ForMember`/`ForPath` mapping function returns a value incompatible with the destination property
- Fixed `dotnet pack` failing on a clean multi-targeted tree (`NU5026`) by removing `GeneratePackageOnBuild`
- Added a static HTML documentation site, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`

## [3.0.2] — 2026-08-29

- **Fixed:** if multiple `ForPath` calls targeted nested properties under the same top-level destination property, only one of them was ever applied — all such paths are now applied correctly

## [3.0.1]

- Added `ForPath` and `ForMember` support
- Improved collection mapping (`List<T>`)
- Simplified usage with `Map<TDestination>(TSource)` format
- Support for nested property mapping and configuration profiles
- Refactored internal `Mapper` logic and added DI support

## [2.0.4] and earlier

See git history for the pre-3.0 release line.
