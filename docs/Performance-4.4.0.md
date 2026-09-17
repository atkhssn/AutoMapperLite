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

4.4.0 replaces AutoMapperLite's interpreted-reflection mapping engine (used through 4.0.2) with one that compiles each source/destination type pair into a cached delegate, adds broader collection-destination type support (arrays and common collection interfaces, not just `List<T>`), and — most significantly for throughput — fixes a collection-loop interface-dispatch cost that was quietly limiting collection performance (see §7).

| Benchmark | 4.0.2 | 4.4.0 | Speedup |
|---|---|---|---|
| Simple object (generic API) | 242.0 ns | 27.5 ns | ~8.8x |
| Nested object (generic API) | 424.1 ns | 64.0 ns | ~6.6x |
| 100-item collection | 26,009.1 ns | 779.9 ns | ~33.3x |

## 4. Object Mapping vs. Manual / AutoMapper / Mapster

| Scenario (generic API) | Manual | AutoMapperLite | AutoMapper | Mapster |
|---|---|---|---|---|
| Simple | 6.1 ns | 27.5 ns | 63.4 ns | 18.4 ns |
| Medium | 11.9 ns | 32.2 ns | 75.5 ns | 31.1 ns |
| Nested | 17.4 ns | 63.2–64.0 ns | 77.0 ns | 30.8 ns |
| Deeply nested (4 levels) | 33.4 ns | 98.5 ns | 123.2 ns | 68.4 ns |
| Custom | 37.8 ns | 41.2 ns | 83.3 ns | 24.0 ns |

AutoMapperLite beats AutoMapper on all five scenarios. Mapster is faster than AutoMapperLite on raw per-call time in every object scenario (Custom is close: 24.0 ns vs. 41.2 ns).

## 5. Collection Mapping vs. Manual / AutoMapper / Mapster

| Scenario (generic API) | Manual | AutoMapperLite | AutoMapper | Mapster |
|---|---|---|---|---|
| 100 items | 499.5 ns | 666.8 ns | 922.7 ns | 553.1 ns |
| 5,000 items | 27.32 µs | 34.04 µs | 43.17 µs | 29.28 µs |

**AutoMapperLite now beats AutoMapper on both collection scenarios** — a reversal from earlier measurements this release cycle (previously AutoMapperLite trailed AutoMapper here), caused by the collection-loop fix in §7, not a methodology change. It remains behind Mapster, though the gap has narrowed substantially (see §7 for the full scaling picture).

## 6. Allocation

| Scenario | Manual | AutoMapperLite | AutoMapper | Mapster |
|---|---|---|---|---|
| Simple | 32 B | 32 B | 32 B | 32 B |
| Medium | 80 B | 80 B | 80 B | 80 B |
| Nested | 80 B | 80 B | 80 B | 80 B |
| Deeply nested | 248 B | 248 B | 248 B | 248 B |
| Custom | 80 B | **104 B** | 80 B | 112 B |
| 100 items (generic API) | 3.96 KB | 3.96 KB | 5.27 KB | 3.96 KB |
| 5,000 items (generic API) | 195.37 KB | 195.37 KB | 284.53 KB | 195.37 KB |
| Cold start | — | 7.9 KB | 20.02 KB | 95.38 KB |

The generic collection API's allocation now matches Manual/Mapster exactly at both sizes shown — a byproduct of the same loop fix in §7 (avoiding a boxed enumerator removes a small allocation, not just time). Custom mapping's 24 B gap is understood and documented — see §9.

## 7. Optimization: Collection Loop Specialization

**Problem.** `Mapper.BuildListMapper` (backing every collection-mapping call) cast the source to `IEnumerable<TSourceItem>` and used `foreach`. For a `List<T>` source — the overwhelmingly common case — this forces `GetEnumerator()` through the interface, which boxes `List<T>`'s own struct enumerator once per call. Separately, the per-item mapper-resolution check (`compiledMap ??= ...`) ran inside the loop on every iteration; since the resolved delegate is stored in a closure field shared across threads, the JIT cannot safely cache that field read in a register across loop iterations the way it could a true local.

**Root cause, confirmed by measurement, not assumed:** interface-dispatch/enumerator-boxing overhead, not the outer per-call dispatch cost that earlier releases' collection-scaling analysis had assumed was the dominant factor.

**Fix.** The loop now pattern-matches `List<TSourceItem>` and `TSourceItem[]` first, indexing directly (`list[i]` / `array[i]`), and falls back to the general `IEnumerable<TSourceItem>` enumerator only for other source shapes. The per-item delegate is resolved once, immediately before whichever loop starts (still lazily — an empty collection never reaches the resolution line, preserving the documented "empty collection needs no registered map" behavior). The identical fix was applied to `Mapper.BuildTypedListEntryPoint` (the generic collection API added earlier in the 4.4.0 cycle), which had the same shape.

**Measured impact** (same benchmark scenarios, before/after, same hardware):

| Scenario | Before | After | Change |
|---|---|---|---|
| 100-item collection (object API) | ~1,301 ns | 779.9 ns | ~40% faster |
| 100-item collection (generic API) | ~1,222 ns | 666.8 ns | ~45% faster |
| 5,000-item collection (object API) | ~59.6–74 µs | 34.90 µs | ~41–53% faster |
| 5,000-item collection (generic API) | ~61 µs | 34.04 µs | ~44% faster |
| 100-item allocation (generic API) | ~4.0 KB | 3.96 KB | now matches Manual exactly |
| 5,000-item allocation (generic API) | ~195.4 KB | 195.37 KB | now matches Manual exactly |

**Trade-offs.** Three code paths (List/array/general-enumerable) instead of one, in two methods. No allocation regression; no cold-start impact (this is warm-path-only code, unrelated to plan compilation). No correctness change — verified via `--verify` and the full existing test suite (156 tests) with no modifications needed to either.

**Why it was kept:** large, real, measured, correctness-preserving improvement with no discovered trade-off.

## 8. Collection Scaling (10 → 100,000 items, after the fix in §7)

| Size | Manual | AutoMapperLite | AutoMapper | Mapster | AML time ratio vs Manual |
|---|---|---|---|---|---|
| 10 | 66.9 ns / 456 B | 160.5 ns / 520 B | 163.2 ns / 648 B | 81.5 ns / 456 B | 2.40x |
| 1,000 | 4.97 µs / 40.06 KB | 6.75 µs / 40.12 KB | 7.24 µs / 48.60 KB | 5.26 µs / 40.06 KB | 1.36x |
| 10,000 | 68.7 µs / 400.06 KB | 83.5 µs / 400.14 KB | 297.4 µs / 582.47 KB | 72.0 µs / 400.06 KB | 1.22x |
| 100,000 | 3.02 ms / 4,000.09 KB | 3.27 ms / 4,000.23 KB | 3.97 ms / 5,297.63 KB | 3.04 ms / 4,000.09 KB | 1.08x |

Before the §7 fix, these ratios were 3.26x / 2.49x / 1.97x / 1.19x respectively — the fix improved every size, with the largest relative gains at small-to-medium scale (where the fixed per-item overhead being removed is a larger fraction of total time). **AutoMapperLite now beats AutoMapper on time and allocation at every size from 1,000 items upward**, and at 100,000 items is within 8% of Manual mapping's own time and within 0.006% of its allocation — essentially matching hand-written code at scale.

## 9. Known Limitation: Custom Mapping's Extra ~24 B Allocation

`ForMember<TMember>`'s callback parameter is typed `Func<TSource, object?>`, not `Func<TSource, TMember>`, even though `TMember` is already known from the destination expression. For a value-typed member (e.g. an `int`), the compiler boxes the result into `object` on every call — this is the entire, confirmed ~24 B/call gap.

Changing the *existing* overload's signature to `Func<TSource, TMember>` would be a breaking API change: `ForMember` currently allows returning a deliberately mismatched type to get a descriptive runtime exception (a documented, tested behavior), and a stricter signature would reject that at compile time. An *additive* second overload was investigated instead and found genuinely non-breaking — see §12 for why it still wasn't implemented (the blocker is internal storage complexity, not API compatibility).

## 10. Known Limitation: Array-Typed Nested Collection Properties

> **Resolved in 4.4.1.** The limitation described below was specific to the 4.4.0 release covered by this report. 4.4.1 added `MappingPlanCompiler.BuildNestedArrayAssignment`/`Mapper.TryGetListToArrayItemTypes`, a second, array-returning helper alongside the existing `List<T>`-returning one — see `CHANGELOG.md` and `docs/changelog.html` for the 4.4.1 entry. This section is left as-is as a historical record of the 4.4.0 state.

The top-level `Map<TDestination>(object)` API supports array destinations. Same-name nested collection *properties* do not — an array-typed nested property is silently skipped, the same as any other unregistered type mismatch. Producing an array from inside a compiled expression tree would need a second, array-returning helper; not implemented in 4.4.0, since it's a rarer shape than the top-level fix already covers. The nested-property source side also remains constrained to `List<T>`.

## 11. Remaining Bottlenecks

- **Collection mapping time vs. Mapster** — AutoMapperLite's public API dispatches through an `object` boundary at the outer call (once per call, not per item); Mapster's `source.Adapt<T>()` pattern is generic on the caller's own static type and sidesteps this entirely. This is an architectural/API-shape difference, not an unaddressed inefficiency. AutoMapperLite no longer trails AutoMapper on collections (see §5, §8).
- **Custom mapping allocation** (§9) — understood, not fixed without a breaking API change.
- **Array-typed nested collection properties** (§10) — documented scope boundary.

## 12. Rejected Optimization Attempt

**Attempt:** eliminate `ForMember`'s value-type boxing (§9) by adding a second `ForMember<TMember>(destination, Func<TSource, TMember> mapFunc)` overload, letting C# overload resolution automatically prefer the boxing-free shape for ordinary value-returning lambdas while preserving the existing `Func<TSource, object?>` overload for callers intentionally returning a mismatched type.

**Result:** not implemented.

**Why it was rejected:** `MapBuilder<,>.MemberMappings` (the dictionary storing registered callbacks) is `internal` but directly exercised by existing tests (`MapBuilderTests.cs`) that assert on its exact shape (`Dictionary<string, Func<TSource, object?>>`) and invoke stored delegates directly. Supporting a genuinely boxing-free typed overload would require either a second parallel dictionary or a wrapper type, both of which ripple into `MappingPlanCompiler`'s per-property compilation logic and `MapBuilder`'s nested-key indexing (`BuildNestedKeyGroups`), and would need a consistent "last write wins across two storage locations" rule for the rare case of a property configured via both overloads. The complexity added — new dictionary, doubled bookkeeping in two classes, more surface area for subtle bugs — is disproportionate to the benefit: eliminating a 24 B/call allocation in a scenario (`ForMember` with a value-type target) that isn't a competitive bottleneck (Custom mapping already beats AutoMapper by ~2x). Consistent with "do not sacrifice maintainability for a few nanoseconds unless the gain is real and significant."

## 13. Future Opportunities

- The `ForMember` boxing fix from §12 remains available as a genuinely non-breaking, purely additive change if the library's maintainer decides the added internal complexity is worth it — the design was already worked out (see §12) and doesn't require a major-version bump, since it's additive.
- ~~Generalizing nested collection properties to support array destinations~~ — done in 4.4.1 (see the note in §10). Array/interface *sources* (as opposed to destinations) for nested collection properties remain unimplemented — the nested-property source side stays constrained to `List<T>` in both the 4.4.0 and 4.4.1 array-destination helpers.
