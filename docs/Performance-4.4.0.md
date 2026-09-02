# AutoMapperLite 4.4.0 — Performance Report

This is a point-in-time engineering report for the 4.4.0 release — the direct successor to 4.0.2, the last previously published version. For the always-current, narrative version of this data, see `docs/performance.html`.

## 1. Environment

- BenchmarkDotNet v0.15.8
- Windows 11, Intel Core Ultra 7 255U, 2.00 GHz, 14 logical / 12 physical cores, 16 GB RAM
- .NET SDK 10.0.400, running on .NET 8.0.30 (X64 RyuJIT)
- Compared against: AutoMapper 16.2.0, Mapster 10.0.12

## 2. Methodology

- Benchmarks live in `AutoMapperLite.Benchmarks`; run via `dotnet run --project AutoMapperLite.Benchmarks -c Release -- --filter "*"`.
- `--verify` asserts AutoMapperLite, AutoMapper, and Mapster all produce byte-identical output for every scenario before any timing number is trusted.
- One shared mapper instance per library, reused across calls, never rebuilt inside a hot loop.
- Mean-time figures carry normal run-to-run variance and should be read as approximate; allocation figures are deterministic and exact.

## 3. Summary

4.4.0 replaces AutoMapperLite's interpreted-reflection mapping engine (used through 4.0.2) with one that compiles each source/destination type pair into a cached delegate, and adds broader collection-destination type support (arrays and common collection interfaces, not just `List<T>`).

| Benchmark | 4.0.2 | 4.4.0 | Speedup |
|---|---|---|---|
| Simple object (generic API) | 242.0 ns | 17.8 ns | ~13.6x |
| Nested object (generic API) | 424.1 ns | 42.0 ns | ~10.1x |
| 100-item collection | 26,009.1 ns | 1,210.9 ns | ~21.5x |

## 4. Object Mapping vs. Manual / AutoMapper / Mapster

| Scenario (generic API) | Manual | AutoMapperLite | AutoMapper | Mapster |
|---|---|---|---|---|
| Simple | 4.3 ns | 17.8 ns | 54.9 ns | 14.4 ns |
| Medium | 8.5–9.3 ns | 29.2 ns | 49.5 ns | 17.3 ns |
| Nested | 12.5–16.0 ns | 42.0 ns | 53.9 ns | 26.6 ns |
| Deeply nested (4 levels) | 55.0–89.3 ns | 68.1 ns | 91.6 ns | 53.4 ns |
| Custom | 50.3–52.1 ns | 51.5 ns | 132.9 ns | 32.2 ns |

AutoMapperLite beats AutoMapper on all five scenarios. Mapster is faster than AutoMapperLite on raw per-call time in every scenario.

## 5. Allocation

| Scenario | Manual | AutoMapperLite | AutoMapper | Mapster |
|---|---|---|---|---|
| Simple | 32 B | 32 B | 32 B | 32 B |
| Medium | 80 B | 80 B | 80 B | 80 B |
| Nested | 80 B | 80 B | 80 B | 80 B |
| Deeply nested | 248 B | 248 B | 248 B | 248 B |
| Custom | 80 B | **104 B** | 80 B | 112 B |
| 100 items | 3.96 KB | 4.06 KB | 5.27 KB | 3.96 KB |
| 5,000 items | 195.37 KB | 195.48 KB | 284.53 KB | 195.37 KB |
| Cold start | — | 7.9 KB | 20.02 KB | 95.38 KB |

Custom mapping's 24 B gap is understood and documented — see §8.

## 6. Cold Start

| Library | Mean time | Allocated |
|---|---|---|
| AutoMapperLite | 176.6–179.2 µs | 7.9 KB |
| AutoMapper | 342.6–345.9 µs | 20.02 KB |
| Mapster | 416.0–430.1 µs | 95.38 KB |

AutoMapperLite is fastest and lowest-allocating on cold start: it compiles only the one type pair actually requested, lazily.

## 7. Collection Scaling (10 → 100,000 items)

| Size | Manual | AutoMapperLite | AutoMapper | Mapster | AML alloc ratio vs Manual |
|---|---|---|---|---|---|
| 10 | 68.5 ns / 456 B | 222.9 ns / 560 B | 161.9 ns / 648 B | 78.4 ns / 456 B | 1.23x |
| 1,000 | 4.75 µs / 40.06 KB | 11.83 µs / 40.16 KB | 7.14 µs / 48.60 KB | 5.20 µs / 40.06 KB | 1.00x |
| 10,000 | 66.9 µs / 400.06 KB | 131.9 µs / 400.18 KB | 236.8 µs / 582.47 KB | 108.1 µs / 400.06 KB | 1.00x |
| 100,000 | 4.28 ms / 4,000.09 KB | 5.11 ms / 4,000.27 KB | 6.02 ms / 5,297.63 KB | 4.43 ms / 4,000.09 KB | 1.00x |

Allocation ratio to Manual stays at 1.00–1.23x regardless of scale. Time ratio to Manual shrinks as scale grows (3.3x → 1.2x), and AutoMapperLite beats AutoMapper on both time and allocation from 10,000 items upward.

## 8. Known Limitation: Custom Mapping's Extra ~24 B Allocation

`ForMember<TMember>`'s callback parameter is typed `Func<TSource, object?>`, not `Func<TSource, TMember>`, even though `TMember` is already known from the destination expression. For a value-typed member (e.g. an `int`), the compiler boxes the result into `object` on every call — this is the entire, confirmed ~24 B/call gap.

This is deliberately not "fixed": retyping the callback to `Func<TSource, TMember>` is a breaking API change, not a safe internal optimization. `ForMember` currently allows returning a deliberately mismatched type to get a descriptive runtime exception (a documented, tested behavior); a stricter signature would reject that at compile time. A cost this small, tied directly to a load-bearing part of the public API's shape, is not worth a breaking change to remove without an explicit decision from the library's maintainer.

## 9. Known Limitation: Array-Typed Nested Collection Properties

The top-level `Map<TDestination>(object)` API supports array destinations. Same-name nested collection *properties* do not — an array-typed nested property is silently skipped, the same as any other unregistered type mismatch. Producing an array from inside a compiled expression tree would need a second, array-returning helper; not implemented in 4.4.0, since it's a rarer shape than the top-level fix already covers. The nested-property source side also remains constrained to `List<T>`.

## 10. Remaining Bottlenecks

- **Collection mapping time** trails Mapster and, at small/medium scale, AutoMapper, because AutoMapperLite's public API dispatches through an `object` boundary at the outer call — an architectural/API-shape difference from Mapster's static-generic calling convention, not an unaddressed inefficiency.
- **Custom mapping allocation** (§8) — understood, not fixed without a breaking API change.
- **Array-typed nested collection properties** (§9) — documented scope boundary.

## 11. Future Opportunities

- Retyping `ForMember`'s callback to `Func<TSource, TMember>` would eliminate the Custom-mapping boxing cost at the price of breaking the mismatched-type-exception pattern — worth a deliberate major-version discussion, not a silent change.
- Generalizing nested collection properties to support array destinations and array/interface sources would require a second, array-returning compiled-loop helper.
