# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

AutoMapperLite is a lightweight, reflection-based object-to-object mapper for .NET (a small AutoMapper alternative), published as an MIT-licensed NuGet library. The repo also contains an xUnit test project, a BenchmarkDotNet benchmark project, and a static HTML documentation site (`docs/`) alongside the library.

`docs/` is a hand-written, dependency-free static site (`index.html`, `api-reference.html`, `examples.html`, `assets/`) — no build step, no generator. Every code sample in `docs/examples.html` was executed and asserted against the real library before being written down (not just read for plausibility), because the README previously shipped a flagship example that looked reasonable but threw `InvalidOperationException` at runtime — see the `ForMember`/`ForPath` note further down. If you edit an example in `docs/`, re-run it (a throwaway console project referencing `AutoMapperLite.csproj` is enough) before trusting it. `docs/` is not currently deployed anywhere (no GitHub Pages / CI wired up) — it's meant to be opened locally or published later by enabling GitHub Pages against `/docs`.

## Commands

```bash
# Build everything (library, tests, benchmarks)
dotnet build AutoMapperLite.slnx

# Pack the NuGet package — must be `dotnet pack`, not `dotnet build`.
# GeneratePackageOnBuild is deliberately NOT set: with a multi-targeted
# project it triggers a known MSBuild/NuGet ordering bug (NU5026 — "file to
# be packed was not found on disk") when `dotnet pack` is later run directly
# on a clean tree. Plain `dotnet build` no longer produces a .nupkg.
dotnet pack AutoMapperLite.csproj -c Release

# Run the test suite (all TFMs the test project targets)
dotnet test AutoMapperLite.Tests/AutoMapperLite.Tests.csproj

# Run a single test
dotnet test AutoMapperLite.Tests/AutoMapperLite.Tests.csproj --filter "FullyQualifiedName~MapperTests.Map_UsesForMember_ForCustomMapping"

# Run benchmarks (Release build required by BenchmarkDotNet)
dotnet run --project AutoMapperLite.Benchmarks -c Release

# Run the test suite against a specific target framework
dotnet test AutoMapperLite.Tests/AutoMapperLite.Tests.csproj -f net8.0
```

Note: `AutoMapperLite.csproj` explicitly excludes the `AutoMapperLite.Tests/` and `AutoMapperLite.Benchmarks/` folders from its own compile globs (see the `Compile Remove` items) since those projects live as subdirectories of the main project's folder — keep those excludes in sync if the project layout changes.

**Test tooling is xunit.v3 (Microsoft Testing Platform / MTP), not classic xunit 2.x/VSTest.** Two consequences, both load-bearing:
- **Test-executable TFMs are net8.0/net9.0/net10.0 only** — `AutoMapperLite.Tests.csproj` intentionally does *not* include net6.0/net7.0, because `xunit.v3` 4.0.0 and `Microsoft.NET.Test.Sdk` 18.9.0 only support `net472+`/`net8.0+` (stated directly in `xunit.v3`'s own package description). The *library* (`AutoMapperLite.csproj`) still multi-targets `net6.0;net7.0;net8.0;net9.0;net10.0` and is compile-verified on all five — this is a test-tooling floor, not a library constraint. Do not re-add net6.0/net7.0 to the test project without downgrading the test packages (which defeats the point of keeping them current).
- **`dotnet test` requires the repo-root `global.json`** (`{"test":{"runner":"Microsoft.Testing.Platform"}}`). xunit.v3 here is built on MTP v2 (`xunit.v3.mtp-v2`), which drops the legacy VSTest-bridge `dotnet test` path entirely on the .NET 10 SDK — without `global.json`, `dotnet test` hard-errors with "Testing with VSTest target is no longer supported...". Don't remove `global.json` or add `TestingPlatformDotnetTestSupport` (the old bridge property) back; neither restores VSTest-mode compatibility with MTP v2 on SDK 10+, only native MTP mode works. See https://aka.ms/dotnet-test-mtp-error for Microsoft's own migration notes.

The benchmark project stays single-targeted (net8.0); it's a dev-only tool, not shipped.

## Architecture

The library has a small, fixed pipeline: **Profile → MapperConfig → MapBuilder → Mapper**.

- `Interfaces/IMapper.cs`, `Interfaces/IMapperConfig.cs` — public contracts.
- `Core/MapperConfig.cs` — holds all registered mappings in a `ConcurrentDictionary<(Type source, Type dest), object>` keyed by the (source, destination) type pair (thread-safe so a `CreateMap` call can't corrupt state if it ever races with `GetMap`/`HasMap` reads against an already-published singleton config). `CreateMap<TSource, TDestination>()` registers a new `MapBuilder<TSource, TDestination>`; `GetMap<TSource, TDestination>()` retrieves it (throws if missing); `HasMap` checks existence for a given type pair — used by `Mapper` to decide whether a mismatched-type property can be recursively mapped.
- `Mapping/MapBuilder.cs` — fluent builder returned by `CreateMap`. `ForMember(dest => dest.X, src => ...)` and `ForPath(dest => dest.A.B.C, src => ...)` both resolve the destination expression into a dot-delimited string key (e.g. `"A.B.C"`) via `GetMemberPath`, and store a `Func<TSource, object?>` against that key in `MemberMappings`. `ForPath` is currently just an alias for `ForMember` — the "path" behavior comes entirely from how `Mapper` interprets keys containing dots. `GetNestedKeys(topLevelProperty)` returns every registered key nested under a top-level property name, grouped once via a `Lazy<Dictionary<string, List<string>>>` (safe because `MemberMappings` is only ever written during `Profile.Configure()`, before any mapping runs) instead of re-scanning `MemberMappings.Keys` with LINQ on every mapped object.
- `Mapping/Profile.cs` — abstract base class users subclass; `Configure(IMapperConfig)` is where `CreateMap` calls go.
- `Core/Mapper.cs` — does the actual mapping via reflection, at both the public `Map<TDestination>(object? source)` entry point and the private generic-typed workers:
  - If `TDestination` is `List<T>`, it iterates the source `IEnumerable` and maps each element via a dynamically-constructed generic `MapSingle<TSource,TDestination>` call (source's *runtime* type is used as the generic arg, not the static type).
  - `MapSingle<TSource, TDestination>` iterates the destination type's public instance properties and resolves each one in priority order: (1) an exact `MemberMappings` key match (`ForMember`), (2) a key that starts with `"PropName."` (nested `ForPath` mapping, handled by `ApplyNestedMapping`), (3) auto-map from a same-named source property — recursing into `Map<>` when the property types differ and a map is registered via `HasMap`, either for the two property types directly, or (for `List<T>` properties) for their item types; otherwise silently skipped.
  - `ApplyNestedMapping` walks a dot-delimited path segment by segment, creating intermediate objects with `Activator.CreateInstance` as needed and applying any `MemberMappings` entry that matches the accumulated path prefix at each level.
  - Property access is still reflection-based (`PropertyInfo.GetValue`/`SetValue` — no compiled expression trees or delegate generation), but the *resolution* of that reflection metadata is cached process-wide in static `ConcurrentDictionary` fields: `GetProperties()` results per `Type`, a source-properties-by-name `Dictionary` per `Type` (avoids repeated `GetProperty(name)` lookups and sidesteps `AmbiguousMatchException` on shadowed `new` members — last one wins), and the closed generic `MethodInfo` for `MapSingle<,>`/`Map<>` per type pair (avoids repeated `MakeGenericMethod` + `GetMethod` calls, which were previously re-done on *every single element* of a mapped collection). All mapped types must have a public parameterless constructor since instances are created via `Activator.CreateInstance`.
  - `MapSingle` holds its destination as `object` (boxed once) rather than as a `TDestination` local, so that when `TDestination` is a value type, repeated `PropertyInfo.SetValue` calls mutate the one shared box instead of discarding each other on transient boxed copies.
  - Custom mapping functions (`ForMember`/`ForPath`) run through `SetMappedValue`, which wraps `PropertyInfo.SetValue` and rethrows a descriptive `InvalidOperationException` (naming the destination path and the mismatched types) instead of a bare reflection `ArgumentException` when a user-supplied lambda returns an incompatible value.
- `Extensions/ServiceCollectionExtensions.cs` — `AddAutoMapperLite(Assembly)` DI entry point: scans the given assembly for concrete `Profile` subclasses with a parameterless constructor, instantiates each and calls `Configure` against a single shared `MapperConfig`, then registers `IMapperConfig` and `IMapper`/`Mapper` **both as singletons** — `Mapper` holds no per-request state beyond the (already-singleton) config, so scoping it would only add allocation for no benefit.

## Key constraints to preserve when modifying

- Target frameworks are `net6.0;net7.0;net8.0;net9.0;net10.0` with nullable reference types and implicit usings enabled. `Microsoft.Extensions.DependencyInjection.Abstractions` is kept at the latest version (`10.0.11` as of this writing) per explicit instruction to never downgrade packages; it declares (and would otherwise warn on) "not tested" support for net6.0/net7.0, which is suppressed via `SuppressTfmSupportBuildWarnings` in `AutoMapperLite.csproj` — verified benign by an actual runtime smoke test (DI container built, `AddAutoMapperLite` resolved, a real `Map<T>` call executed) on net6.0, not just assumed from the warning text.
- Packing requires `dotnet pack -c Release`, not `dotnet build` (see the note under Commands above) — bump `Version` in `AutoMapperLite.csproj` (and update `PackageReleaseNotes`) when shipping a change intended for release.
- Public API surface (`IMapper`, `IMapperConfig`, `MapBuilder<,>`, `Profile`) is what consuming projects bind against directly; changes here are breaking changes for NuGet consumers. `IMapper.Map<TDestination>`'s `source` parameter is `object?` (not `object`) — this is intentionally nullable since the method has always accepted and handled `null` at runtime.
- `MapBuilder<,>.MemberMappings` is `internal`; `AutoMapperLite.csproj` grants `AutoMapperLite.Tests` access via `InternalsVisibleTo` so tests can assert on registered mapping keys directly.
- `Mapper.MapSingle` applies *every* `MemberMappings` key prefixed with a given top-level destination property to the same nested instance (see `AutoMapperLite.Tests/MapperTests.cs::Map_UsesForPath_ForDeeplyNestedDestination`), so multiple `ForPath` calls under the same top-level property all take effect. Preserve this when touching the nested-mapping branch in `Core/Mapper.cs`.
- The reflection-metadata caches in `Core/Mapper.cs` (`MapSingleMethodCache`, `MapMethodCache`, `PublicPropertiesCache`, `PropertiesByNameCache`) are `static` and process-wide by design (`Type` → metadata is a process-wide invariant), not per-`Mapper`-instance — don't move them to instance fields, that would defeat the point of caching across DI-resolved `Mapper` instances/scopes.
- A source item that is `null` inside a collection passed to `Map<List<TDestination>>` will `NullReferenceException` on `item.GetType()` — this is a pre-existing limitation, not something recently introduced; treat it as a known edge case rather than "fixing" it silently if you touch that loop, since there's no single obviously-correct behavior for a null list element (skip it? add `null` to the result? both are behavior changes).
- `ForMember`/`ForPath` functions must return the *already-mapped* value, not the raw source sub-object — the library never implicitly converts types inside a custom mapping function. When source/destination property types differ, the function has to call back into a `Mapper` itself (e.g. `src => mapper.Map<CountryViewModel>(src.Country)`, capturing a `Mapper` built from the same `config` in the closure). The README's flagship "Create Mapping Profiles" example previously got this wrong (passed the raw `Country` straight through) and threw `InvalidOperationException` at runtime for *anyone who copy-pasted it* — verified by actually running it, not just by reading the code. If you touch that example again, re-run it, don't just eyeball it.
- **A `null` source property value is skipped, not assigned** (`if (value == null) continue;` in the auto-map branch of `MapSingle`) — a destination's own default (field initializer, etc.) is preserved rather than overwritten with `null`. This is easy to miss because a same-typed round trip can't distinguish "skipped" from "assigned null" (both look like null); `MapperTests.Map_SkipsAssignment_WhenSourcePropertyIsNull` uses a destination with a *non-null* default specifically to make the distinction observable. This differs from AutoMapper's default (which does overwrite with null) — worth calling out if anyone asks why a null source field didn't clobber a computed default.
- **Auto-map only fires on an exact `Type` match** (or a registered map). Two *different* enum types with identical member names, `enum → string`, and numeric widening (`int → long`, etc.) are all silently left at the destination's default — not converted, not thrown. This is a deliberate scope boundary (no convention/coercion engine, consistent with the "lightweight, not AutoMapper" positioning — see `docs/comparison.html`), not a bug; don't "fix" it by quietly adding conversion logic without discussing the API/behavior change first.
- **Destination types need a public parameterless constructor** — instances are created via `Activator.CreateInstance`. Positional records (`record Foo(int A)`) and any class requiring constructor arguments throw (wrapped by `CreateInstance` into a clear `InvalidOperationException` naming the type and explaining why, instead of a bare `MissingMethodException`). Records *with* a parameterless constructor and `{ get; init; }` properties work fine — reflection's `PropertyInfo.SetValue` can call an init accessor even though the C# compiler wouldn't let you call it directly (the `init` restriction is compile-time only). No constructor-parameter mapping is implemented.
