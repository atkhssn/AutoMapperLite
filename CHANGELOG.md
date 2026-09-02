# Changelog

All notable changes to AutoMapperLite are documented here.

## [4.4.0] — 2026-09-03 — Current official release

**Performance and mapping engine redesign.** The mapping engine now compiles each source/destination type pair into a cached delegate via `System.Linq.Expressions` on first use, instead of interpreting mappings through reflection (`PropertyInfo.GetValue`/`SetValue`, `MethodInfo.Invoke`, `Activator.CreateInstance`). Dispatch — finding the right compiled delegate for a given call — is cached per `Mapper` instance, collections resolve their element mapper once per call instead of once per item, and nested same-name properties resolve their mapping plan at compile time instead of at runtime. See [Performance](https://automapperlite.atkhssn.info/performance.html) for full measured results.

- **Added:** `IMapper.Map<TSource, TDestination>(TSource source)` — a strongly-typed overload for call sites where the source type is known at compile time. This is the fastest way to call AutoMapperLite: it skips runtime type resolution and return-value boxing entirely.
- **Added:** collection destinations are no longer limited to `List<T>`. `Map<TDestination>(object)` now also accepts `T[]`, `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, and `IEnumerable<T>`. The same flexibility applies to same-name nested collection properties.
- **Behavior change (disclosed):** exceptions raised during mapping now propagate directly as `InvalidOperationException` instead of being wrapped in `System.Reflection.TargetInvocationException`.
- **Behavior change (disclosed):** `List<T>` mapping resolves the item map from the source collection's declared item type rather than each element's runtime type. A `List<Base>` containing `Derived` instances now maps every element through `Base`'s registered map.
- Allocations for simple, medium, nested, and deeply-nested object mapping now match hand-written manual mapping exactly. Collection mapping allocation is within a few percent of manual mapping/Mapster at any collection size, from 10 items to 100,000.
- No public API breaking changes since 4.0.2 (only for anyone implementing `IMapper` directly, rather than using the built-in `Mapper` class, due to the new interface member above).

## [4.0.2] — 2026-08-29

- **Fixed:** the test project's tooling (`xunit.v3`, `Microsoft.NET.Test.Sdk` 18.9.0) only supports net8.0+; `AutoMapperLite.Tests` now targets `net8.0;net9.0;net10.0` (the library itself is unaffected and still targets `net6.0;net7.0;net8.0;net9.0;net10.0`).
- **Fixed:** `Activator.CreateInstance` failures (e.g. mapping to a positional record with no parameterless constructor) now throw a clear `InvalidOperationException` naming the type, instead of a bare `MissingMethodException`.
- Added real, measured BenchmarkDotNet comparisons against manual mapping, AutoMapper, and Mapster (see [Performance](https://automapperlite.atkhssn.info/performance.html)).
- Verified and documented previously-untested behaviors: a `null` source property is skipped (not assigned), auto-map requires an exact type match, and records with a parameterless constructor + init-only properties map correctly.

## [4.0.1] — 2026-08-29

Official rebrand and release. All versions prior to 4.0.1 are deprecated.

- Official project website: [automapperlite.atkhssn.info](https://automapperlite.atkhssn.info)
- New visual identity and full XML documentation on the public API.
- NuGet package metadata overhauled: accurate description, project URL, discoverability tags.
- Static documentation site: Getting Started guide, usage guide, performance page, feature comparison, FAQ.
- Carries forward every fix from the 3.0.x line below — no public API breaking changes since 3.0.3.

## [3.0.3] — 2026-08-29

- Now multi-targets `net6.0`–`net10.0` (previously `net8.0` only).
- **Fixed:** mapping to a value-type (struct) destination silently produced an unpopulated struct.
- **Fixed:** a `List<T>` destination property was never auto-mapped even when a map was registered for the item types.
- Hardened `MapperConfig` and DI registration for concurrent use.
- Reduced mapping-hot-path allocations and reflection overhead by caching resolved `PropertyInfo`/`MethodInfo`.
- Clearer exception when a `ForMember`/`ForPath` function returns an incompatible value.
- Added a static HTML documentation site, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`.

## [3.0.2] — 2026-08-29

- **Fixed:** multiple `ForPath` calls targeting nested properties under the same top-level destination property now all apply correctly (previously only one took effect).

## [3.0.1]

- Added `ForPath` and `ForMember` support.
- Improved collection mapping (`List<T>`).
- Simplified usage with `Map<TDestination>(TSource)`.
- Support for nested property mapping and configuration profiles.
- Refactored internal `Mapper` logic and added DI support.

## [2.0.4] and earlier

See git history for the pre-3.0 release line.
