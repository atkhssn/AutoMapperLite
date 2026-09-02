# Changelog

All notable changes to AutoMapperLite are documented here.

## [4.3.0] — 2026-09-03 — Current official release

- **Fixed a per-call closure allocation that was the single largest source of AutoMapperLite's excess allocation versus manual mapping and Mapster.** `MapBuilder.GetCompiledMap` (the method every dispatch path funnels through to reach the compiled per-type-pair delegate) wrote `LazyInitializer.EnsureInitialized(ref _compiledMap, () => MappingPlanCompiler.Compile(this, config))` directly in its body. That lambda captures `this` and `config`, so the C# compiler must materialize a new closure object plus a new delegate wrapping it at the call site *before* `EnsureInitialized` ever gets a chance to decide whether to invoke it — meaning every single call paid for that allocation, including the overwhelming-majority warm-path case where the factory is never actually run. This hit every same-name nested single-object property (via `MappingPlanCompiler.BuildNestedObjectAssignment`'s `builder.Map(...)` call, executed once per parent object mapped) and the typed `IMapper.Map<TSource,TDestination>` overload directly. Fixed by checking `_compiledMap` with a `Volatile.Read` first and only falling through to the lambda-allocating path once, ever, per builder. Measured impact (same BenchmarkDotNet suite): Simple/Medium/Nested/DeepNested object mapping now allocate *exactly* what Manual mapping does (32 B/80 B/80 B/248 B, down from 128 B/176 B/272 B/632 B); a 100-item collection dropped from 13.44 KB to 4.06 KB (was 3.4x Manual's/Mapster's 3.96 KB, now within 3%); a 5,000-item collection dropped from 664.25 KB to 195.48 KB (was 3.4x Manual's/Mapster's 195.37 KB, now within 0.06%). Verified this holds at scale: at 100,000 items, AutoMapperLite's allocation is now within 0.007% of Manual mapping's (previously ~3.4x at any size), and its time is within 11% of Manual's, actually *beating* AutoMapper by 18% at that size.
- **Fixed `IMapper.Map<TSource, TDestination>(TSource)` (added in 4.2.0) being measurably *slower* than the untyped `Map<TDestination>(object)` overload it was explicitly designed to beat.** Each call still went through `IMapperConfig.GetMap<TSource,TDestination>()` — a plain, uncached-at-the-Mapper-level dictionary lookup — plus the closure allocation described above, while the untyped overload's cached dispatch closure captured an already-resolved compiled delegate directly (no re-lookup, no volatile read needed). Added a dedicated per-`Mapper`-instance cache (`_typedEntryPoints`, mirroring the untyped path's own `_entryPoints`) so the typed overload now has the identical shape: one dictionary lookup keyed by compile-time `Type`, then a direct delegate invocation, with zero return-value boxing either way.
- Added a regression test (`Map_HandlesSelfReferentialType_WithAcyclicData`) guarding against a further optimization that was considered and *deliberately not made*: eagerly resolving a nested property's compiled delegate at the parent's plan-compile time (rather than lazily, at first actual use) would StackOverflow on self-referential types like `Employee.Manager: Employee`, even with fully acyclic data, by recursing into compiling the same type's plan again before ever looking at real data. This is now documented in `CLAUDE.md` as a permanent constraint on future dispatch-layer changes.
- No public API or behavior changes since 4.2.0 — this release is dispatch-layer allocation/performance work only. Verified against the full existing test suite (120 tests across net8.0/9.0/10.0) plus the new regression test above, and against the `--verify` cross-library output-equality harness.

## [4.2.0] — 2026-08-29

- **Dispatch-layer performance redesign.** 4.1.0 compiled each mapping into an expression-tree delegate but still paid three avoidable runtime-dispatch costs on top of that compiled work: (1) the untyped `Map<TDestination>(object)` entry point re-resolved `config.GetMap<TSource,TDestination>()` — a dictionary lookup — on *every single call*, in addition to the cache lookup that found the wrapper in the first place; (2) `List<T>` mapping resolved the item mapper via `item.GetType()` plus a dictionary lookup for *every element*; (3) nested same-name auto-mapped properties (single object and `List<T>`) routed through that same per-call runtime dispatch despite the nested type pair being fully known at plan-compile time. 4.2.0 fixes all three:
  - `Mapper`'s entry-point cache moved from a global, config-agnostic cache to a per-`Mapper`-instance cache that resolves the builder **once** or the entry-point closure now captures it directly — one dictionary lookup instead of two per call, with no cross-config risk since a `Mapper` wraps exactly one config for its lifetime.
  - `List<T>` mapping now determines the source collection's item type once (via `List<T>`'s generic argument, an array's element type, or the `IEnumerable<T>` it implements) and runs a strongly-typed `foreach` loop with zero per-item `GetType()`, zero per-item dictionary lookup, and zero boxing of value-typed items. Source collections whose item type can't be determined this way (non-generic `IEnumerable`) still dispatch per element by runtime type, now with a one-slot cache so consecutive same-typed elements avoid re-querying the dictionary.
  - `MappingPlanCompiler` now resolves nested `MapBuilder`s (for same-name mismatched-type properties, both single objects and matching `List<T>` item types) once, at plan-compile time, and bakes the actual builder reference into the compiled expression as a constant — the compiled parent delegate calls straight into the nested builder's own compiled delegate.
  - Measured on this repo's own BenchmarkDotNet suite (see `docs/performance.html` for full scenario-by-scenario numbers and methodology): simple object 67.6 ns → 42.2 ns (~38% faster, now *faster* than AutoMapper's 58.2 ns); 100-item collection 6,986.7 ns → 2,744.1 ns (~61% faster); 5,000-item collection 390.81 µs → 156.47 µs (~60% faster). Cold start remains the fastest of the three libraries compared.
- **Added:** `IMapper.Map<TSource, TDestination>(TSource source)` — a strongly-typed overload for call sites where the source type is known at compile time; skips `source.GetType()`, the entry-point cache, and return-value boxing entirely. Additive for callers; technically breaking for anyone implementing `IMapper` themselves (rather than using the built-in `Mapper` class), since it's a new interface member.
- **Behavior change (disclosed):** `List<T>` destination mapping now resolves the item map from the source collection's *declared* item type, not each element's individual runtime type. A `List<Base>` containing `Derived` instances now maps every element through `Base`'s registered map (using `Base`'s properties only) instead of dispatching per element by runtime type — which previously threw for a `Derived` element unless `(Derived, TDestItem)` was separately registered. Source collections whose item type can't be determined from their own `Type` are unaffected.
- No public API breaking changes for callers since 3.0.3 (only for custom `IMapper` implementers, per the addition above).

## [4.1.0] — 2026-08-29

- **Performance redesign:** the mapping engine now compiles each source/destination type pair into a cached `Func<TSource, TDestination>` via `System.Linq.Expressions` the first time it's mapped, instead of interpreting the mapping through `PropertyInfo.GetValue`/`SetValue`, `MethodInfo.Invoke`, and `Activator.CreateInstance` on every call. Measured on this repo's own BenchmarkDotNet suite (see `docs/performance.html` for full scenario-by-scenario numbers and methodology):
  - Simple object: 242.0 ns → 67.6 ns (~3.6x faster)
  - Nested object: 424.1 ns → 137.8 ns (~3.1x faster)
  - 100-item collection: 26,009.1 ns → 6,986.7 ns (~3.7x faster)
  - Cold start (first mapping call, including compilation): AutoMapperLite is now faster than both AutoMapper's and Mapster's own cold start
  - AutoMapperLite is still slower than AutoMapper and Mapster on warm, repeated auto-mapped calls (especially collections, where both alternatives compile their whole plan upfront) — see the Comparison and Performance pages for the honest breakdown of where each library wins.
- **Behavior change (disclosed, not a breaking API change):** exceptions raised during mapping (e.g. a `ForMember` function returning an incompatible type, or a destination with no parameterless constructor) now propagate directly as `InvalidOperationException` instead of being wrapped in `System.Reflection.TargetInvocationException`. The wrapping was an implementation detail of the old reflection-based dispatch, never part of the documented contract.
- `ForPath` nested-path mapping is unchanged (still interpreted) — profiling showed it wasn't a hot path, and compiling arbitrary-depth dynamic paths correctly wasn't worth the added complexity and risk.
- Added a new `MappingPlanCompiler` (`Core/MappingPlanCompiler.cs`); compiled plans are cached per `MapBuilder` instance (not globally by type pair) so two different `IMapperConfig` instances mapping the same types differently can never share a stale plan. Compilation is thread-safe via `LazyInitializer.EnsureInitialized`, covered by a dedicated 500-thread race test.
- No public API breaking changes since 3.0.3.

## [4.0.2] — 2026-08-29

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
