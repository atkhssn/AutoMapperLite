# Changelog

All notable changes to AutoMapperLite are documented here. Versions and dates below are verified against the package's actual published history on [NuGet](https://www.nuget.org/packages/AutoMapperLite), not reconstructed from memory or from this repo's local version-bump commits (which, for the 2026-08-29 development session, briefly reused the numbers `3.0.2`/`3.0.3`/`4.0.1` locally before the work was actually packed and published as `4.0.2` — see the note on that release below).

## [4.4.0] — 2026-09-03 — Current official release

**Performance and mapping engine redesign.** The mapping engine now compiles each source/destination type pair into a cached delegate via `System.Linq.Expressions` on first use, instead of interpreting mappings through reflection (`PropertyInfo.GetValue`/`SetValue`, `MethodInfo.Invoke`, `Activator.CreateInstance`). Dispatch — finding the right compiled delegate for a given call — is cached per `Mapper` instance, collections resolve their element mapper once per call instead of once per item, and nested same-name properties resolve their mapping plan at compile time instead of at runtime. See [Performance](https://automapperlite.atkhssn.info/performance.html) for full measured results.

- **Added:** `IMapper.Map<TSource, TDestination>(TSource source)` — a strongly-typed overload for call sites where the source type is known at compile time. This is the fastest way to call AutoMapperLite: it skips runtime type resolution and return-value boxing entirely. This overload now also works for collections directly — `mapper.Map<List<TSourceItem>, List<TDestItem>>(sourceList)` skips resolving the source collection's runtime type.
- **Added:** collection destinations are no longer limited to `List<T>`. `Map<TDestination>(object)` now also accepts `T[]`, `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, and `IEnumerable<T>`. The same flexibility applies to same-name nested collection properties.
- **Performance:** fixed a collection-loop interface-dispatch cost that was limiting collection-mapping throughput — casting the source to `IEnumerable<T>` and using `foreach` forced `List<T>`'s own struct enumerator to be boxed through the interface on every call. The loop now indexes `List<T>`/arrays directly. Measured: a 100-item collection dropped from ~1.3 µs to ~0.78 µs (object API) / ~0.67 µs (generic API); a 5,000-item collection dropped from ~60–74 µs to ~35 µs. AutoMapperLite now beats AutoMapper on collection mapping at every size from 1,000 items upward (previously it trailed), and at 100,000 items is within 8% of hand-written manual mapping's own time. See [Performance](https://automapperlite.atkhssn.info/performance.html) for full before/after figures.
- **Behavior change (disclosed):** exceptions raised during mapping now propagate directly as `InvalidOperationException` instead of being wrapped in `System.Reflection.TargetInvocationException`.
- **Behavior change (disclosed):** `List<T>` mapping resolves the item map from the source collection's declared item type rather than each element's runtime type. A `List<Base>` containing `Derived` instances now maps every element through `Base`'s registered map.
- Allocations for simple, medium, nested, and deeply-nested object mapping now match hand-written manual mapping exactly. Collection mapping allocation now matches manual mapping/Mapster exactly via the generic API, and is within a few percent via the object API, at any collection size from 10 items to 100,000.
- No public API breaking changes since 4.0.2 (only for anyone implementing `IMapper` directly, rather than using the built-in `Mapper` class, due to the new interface member above).
- Not currently flagged deprecated on NuGet (it's the current release). Engineering notes elsewhere in this repo refer to intermediate `4.1.0`/`4.2.0`/`4.3.0` milestones — those were internal-only labels for stages of this same body of work and were never independently published to NuGet; only 4.4.0 shipped.

## [4.0.2] — 2026-08-29

Development resumed on this date after the project had been dormant since the 3.2.1 release on 2025-07-09 — a gap of roughly 13.5 months. Not currently flagged deprecated on NuGet (unlike every 1.0.0–3.2.1 release below), despite being superseded by 4.4.0.

Published NuGet release notes for this version:
- **Fixed:** the test project's tooling (`xunit.v3`, `Microsoft.NET.Test.Sdk` 18.9.0) only supports net8.0+; `AutoMapperLite.Tests` now targets `net8.0;net9.0;net10.0` (the library itself is unaffected and still targets `net6.0;net7.0;net8.0;net9.0;net10.0`), and a repo-root `global.json` opts `dotnet test` into native Microsoft Testing Platform mode.
- **Fixed:** `Activator.CreateInstance` failures (e.g. mapping to a positional record with no parameterless constructor) now throw a clear `InvalidOperationException` naming the type, instead of a bare `MissingMethodException`.
- Added real, measured BenchmarkDotNet comparisons against manual mapping, AutoMapper 16.2.0, and Mapster 10.0.12 (see [Performance](https://automapperlite.atkhssn.info/performance.html)); AutoMapperLite was not the fastest of the four at this point, and the docs/package description no longer implied otherwise.
- Verified and documented previously-untested behaviors: a `null` source property is skipped (not assigned), auto-map requires an exact type match, and records with a parameterless constructor + init-only properties map correctly.

Also carried in this release, done during the same pre-publish development session but not itemized in the terse NuGet release notes above (confirmed from this repo's own commit history rather than from NuGet, since it was never split out as its own package):
- Now multi-targets `net6.0`–`net10.0` (the 3.0.1–3.2.1 line published in 2025 was net8.0-only).
- Fixed: mapping to a value-type (struct) destination silently produced an unpopulated struct.
- Fixed: a `List<T>` destination property was never auto-mapped even when a map was registered for the item types.
- Fixed: multiple `ForPath` calls targeting nested properties under the same top-level destination property now all apply (previously only one took effect).
- Hardened `MapperConfig` and DI registration for concurrent use; reduced mapping-hot-path allocations and reflection overhead by caching resolved `PropertyInfo`/`MethodInfo`.
- Added a static HTML documentation site, official rebrand, full XML documentation on the public API, and overhauled NuGet package metadata.

This work passed through the local version numbers `3.0.2` (reused — see note at top of this file), `3.0.3`, and `4.0.1` in this repo's commit history over the course of the same day before actually being packed and published to NuGet — none of those three numbers were ever independently published; 4.0.2 is the only release that resulted from this line of work.

## [3.2.1] — 2025-07-09 — Deprecated (critical bugs)

Published, listed on NuGet, and flagged deprecated (`CriticalBugs`). Depends on `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.7. Published release notes are identical, verbatim, to 2.0.4's (see note under that version) — no changelog-visible differentiation from the four releases before it.

## [3.1.1] — 2025-07-09 — Deprecated (critical bugs)

Published, listed on NuGet, and flagged deprecated (`CriticalBugs`). Same dependency and release-notes text as 3.2.1.

## [3.0.2] — 2025-07-09 — Deprecated (critical bugs)

Published, listed on NuGet, and flagged deprecated (`CriticalBugs`). Same release-notes text as 2.0.4. **Not** the same release as the `3.0.2` version number that briefly existed in this repo's local csproj during the 2026-08-29 development session (that local bump was never published — see the 4.0.2 entry above); this is the real, originally-published 3.0.2 from 2025.

## [3.0.1] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted on NuGet (hidden from search, still installable by exact version) and flagged deprecated (`CriticalBugs`). Same release-notes text as 2.0.4.

## [2.0.8] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). Same release-notes text as 2.0.4.

## [2.0.7] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). Same release-notes text as 2.0.4.

## [2.0.6] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). Same release-notes text as 2.0.4.

## [2.0.5] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). Same release-notes text as 2.0.4.

## [2.0.4] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). Release notes as published:
- Added `ForPath` and `ForMember` support.
- Improved collection mapping (`List<T>`).
- Simplified usage with `Map<TDestination>(TSource)` format.
- Support for nested property mapping and configuration profiles.
- Refactored internal `Mapper` logic and added DI support.

This exact text was then republished verbatim, unchanged, through the next eight releases (2.0.5 through 3.2.1) — a real quirk of the published history, not a transcription error in this file.

## [2.0.3] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). No release notes were published for this version. Added a package icon (`icon.png`).

## [2.0.1] — 2025-07-05 — Deprecated (critical bugs), unlisted

Published but unlisted, flagged deprecated (`CriticalBugs`). Retargeted from net6.0 to net8.0 and added a dependency on `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.6. Release notes as published: "Initial release of AutoMapperLite v2.0.1. Dependency Injection (DI) support. Nested model mapping support. Lightweight and reflection-based. No third-party dependencies." (The "no third-party dependencies" line is as originally published; it's already in tension with the DI Abstractions dependency added in this same version.)

## [1.0.0] — 2025-07-03 — Deprecated (critical bugs), unlisted

The first published release. Targeted `net6.0` only, with no DI package dependency. No release notes were published for this version.
