# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

AutoMapperLite is a lightweight, reflection-based object-to-object mapper for .NET (a small AutoMapper alternative), published as an MIT-licensed NuGet library. The repo also contains an xUnit test project and a BenchmarkDotNet benchmark project alongside the library.

## Commands

```bash
# Build everything (library, tests, benchmarks)
dotnet build AutoMapperLite.slnx

# Pack the NuGet package (also happens automatically on build of the main
# project, since GeneratePackageOnBuild is true in AutoMapperLite.csproj)
dotnet pack AutoMapperLite.csproj -c Release

# Run the test suite
dotnet test AutoMapperLite.Tests/AutoMapperLite.Tests.csproj

# Run a single test
dotnet test AutoMapperLite.Tests/AutoMapperLite.Tests.csproj --filter "FullyQualifiedName~MapperTests.Map_UsesForMember_ForCustomMapping"

# Run benchmarks (Release build required by BenchmarkDotNet)
dotnet run --project AutoMapperLite.Benchmarks -c Release
```

Note: `AutoMapperLite.csproj` explicitly excludes the `AutoMapperLite.Tests/` and `AutoMapperLite.Benchmarks/` folders from its own compile globs (see the `Compile Remove` items) since those projects live as subdirectories of the main project's folder — keep those excludes in sync if the project layout changes.

## Architecture

The library has a small, fixed pipeline: **Profile → MapperConfig → MapBuilder → Mapper**.

- `Interfaces/IMapper.cs`, `Interfaces/IMapperConfig.cs` — public contracts.
- `Core/MapperConfig.cs` — holds all registered mappings in a `Dictionary<(Type source, Type dest), object>` keyed by the (source, destination) type pair. `CreateMap<TSource, TDestination>()` registers a new `MapBuilder<TSource, TDestination>`; `GetMap<TSource, TDestination>()` retrieves it (throws if missing); `HasMap` checks existence for a given type pair — used by `Mapper` to decide whether a mismatched-type property can be recursively mapped.
- `Mapping/MapBuilder.cs` — fluent builder returned by `CreateMap`. `ForMember(dest => dest.X, src => ...)` and `ForPath(dest => dest.A.B.C, src => ...)` both resolve the destination expression into a dot-delimited string key (e.g. `"A.B.C"`) via `GetMemberPath`, and store a `Func<TSource, object?>` against that key in `MemberMappings`. `ForPath` is currently just an alias for `ForMember` — the "path" behavior comes entirely from how `Mapper` interprets keys containing dots.
- `Mapping/Profile.cs` — abstract base class users subclass; `Configure(IMapperConfig)` is where `CreateMap` calls go.
- `Core/Mapper.cs` — does the actual mapping via reflection, at both the public `Map<TDestination>(object source)` entry point and the private generic-typed workers:
  - If `TDestination` is `List<T>`, it iterates the source `IEnumerable` and maps each element via a dynamically-constructed generic `MapSingle<TSource,TDestination>` call (source's *runtime* type is used as the generic arg, not the static type).
  - `MapSingle<TSource, TDestination>` iterates the destination type's public instance properties and resolves each one in priority order: (1) an exact `MemberMappings` key match (`ForMember`), (2) a key that starts with `"PropName."` (nested `ForPath` mapping, handled by `ApplyNestedMapping`), (3) auto-map from a same-named source property — recursing into `Map<>` when the property types differ and a map is registered via `HasMap`, otherwise silently skipped.
  - `ApplyNestedMapping` walks a dot-delimited path segment by segment, creating intermediate objects with `Activator.CreateInstance` as needed and applying any `MemberMappings` entry that matches the accumulated path prefix at each level.
  - All property access is reflection-based (no compiled expression trees/caching), and all mapped types must have a public parameterless constructor since instances are created via `Activator.CreateInstance`.
- `Extensions/ServiceCollectionExtensions.cs` — `AddAutoMapperLite(Assembly)` DI entry point: scans the given assembly for concrete `Profile` subclasses with a parameterless constructor, instantiates each and calls `Configure` against a single shared `MapperConfig`, then registers `IMapperConfig` as a singleton and `IMapper`/`Mapper` as scoped.

## Key constraints to preserve when modifying

- Target framework is `net8.0` with nullable reference types and implicit usings enabled.
- `GeneratePackageOnBuild` is `true`, so every `dotnet build` repacks the NuGet package — bump `Version` in `AutoMapperLite.csproj` (and update `PackageReleaseNotes`) when shipping a change intended for release.
- Public API surface (`IMapper`, `IMapperConfig`, `MapBuilder<,>`, `Profile`) is what consuming projects bind against directly; changes here are breaking changes for NuGet consumers.
- `MapBuilder<,>.MemberMappings` is `internal`; `AutoMapperLite.csproj` grants `AutoMapperLite.Tests` access via `InternalsVisibleTo` so tests can assert on registered mapping keys directly.
- `Mapper.MapSingle` applies *every* `MemberMappings` key prefixed with a given top-level destination property to the same nested instance (see `AutoMapperLite.Tests/MapperTests.cs::Map_UsesForPath_ForDeeplyNestedDestination`), so multiple `ForPath` calls under the same top-level property all take effect. Preserve this when touching the nested-mapping branch in `Core/Mapper.cs`.
