using AutoMapperLite.Interfaces;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace AutoMapperLite
{
    /// <summary>
    /// Default <see cref="IMapper"/> implementation. Effectively stateless from a caller's
    /// perspective beyond the injected <see cref="IMapperConfig"/> — the per-instance caches
    /// below only ever memoize the *result* of resolving that same config's own registrations,
    /// so a <see cref="Mapper"/> remains safe to use concurrently and safe to register as a
    /// singleton.
    /// </summary>
    /// <remarks>
    /// The actual per-property copying logic for a given (source, destination) type pair is
    /// compiled once, lazily, into a real delegate via <see cref="MappingPlanCompiler"/> and
    /// cached on the owning <see cref="MapBuilder{TSource, TDestination}"/> — see
    /// <c>CLAUDE.md</c> for the full architecture writeup. This type handles runtime type
    /// dispatch (routing an <see cref="object"/> source to the right compiled plan) and
    /// collection iteration for the untyped <see cref="Map{TDestination}(object)"/> API; it
    /// deliberately does not re-resolve reflection metadata per call, and — as of the 4.2.0
    /// dispatch redesign — resolves each (source, destination) type pair's <c>MapBuilder</c>
    /// exactly once per <see cref="Mapper"/> instance rather than once per <c>Map</c> call.
    /// </remarks>
    public sealed class Mapper : IMapper
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PublicPropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertiesByNameCache = new();

        private static readonly MethodInfo BuildEntryPointDefinition =
            typeof(Mapper).GetMethod(nameof(BuildEntryPoint), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo BuildTypedEntryPointDefinition =
            typeof(Mapper).GetMethod(nameof(BuildTypedEntryPoint), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo BuildListMapperDefinition =
            typeof(Mapper).GetMethod(nameof(BuildListMapper), BindingFlags.NonPublic | BindingFlags.Instance)!;

        internal static readonly MethodInfo ConvertMappedValueDefinition =
            typeof(Mapper).GetMethod(nameof(ConvertMappedValue), BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static readonly MethodInfo ApplyNestedMappingDefinition =
            typeof(Mapper).GetMethod(nameof(ApplyNestedMapping), BindingFlags.NonPublic | BindingFlags.Static)!;

        private readonly IMapperConfig _config;

        // Per (source, destination) type pair for the untyped Map<TDestination>(object) entry
        // point: resolves config.GetMap<TSource,TDestination>() exactly ONCE (via MakeGenericMethod
        // + a single reflective Invoke — never in the hot path), then every subsequent call for
        // that pair goes straight from this dictionary lookup to the builder's own compiled
        // delegate. The previous design cached a config-agnostic wrapper in a *global* dictionary
        // shared across every Mapper/IMapperConfig instance; that wrapper had to re-resolve
        // config.GetMap<,>() — itself a dictionary lookup — on every single invocation, so every
        // Map call paid for two dictionary lookups instead of one. Scoping the cache to this
        // Mapper instance (which owns exactly one config for its whole lifetime) lets the builder
        // reference be resolved once and reused directly, and is trivially safe for config
        // isolation: two Mapper instances over two different configs simply have two different,
        // never-shared dictionaries.
        private readonly ConcurrentDictionary<(Type Source, Type Destination), Func<object, object?>> _entryPoints = new();

        // Per (source, destination) type pair for the strongly-typed Map<TSource,TDestination>
        // entry point. Measured directly against the untyped cache above: without this, every
        // call went through IMapperConfig.GetMap<TSource,TDestination>() (a *different*
        // dictionary lookup, in MapperConfig, every single call) followed by
        // MapBuilder.GetCompiledMap's Volatile.Read - two extra indirections beyond what the
        // untyped path's cached closure needs, since that closure captures the already-resolved
        // compiled delegate directly (a plain closure-field read needs no volatile fence at all).
        // That made the "fast path" measurably *slower* than the untyped one in practice,
        // contradicting its own purpose. Caching the resolved compiled delegate here too, keyed
        // by the compile-time typeof(TSource)/typeof(TDestination) (never source.GetType()),
        // gives Map<TSource,TDestination> the same shape as the untyped path: one dictionary
        // lookup, then a direct delegate invocation, with zero return-value boxing either way
        // since the delegate is Func<TSource,TDestination> the whole way through.
        private readonly ConcurrentDictionary<(Type Source, Type Destination), Delegate> _typedEntryPoints = new();

        // Per (source item type, destination item type) pair for List<T> destinations: a
        // strongly-typed collection loop, resolved once, that maps every item via a real
        // foreach (TItem item in source) with no per-item GetType()/dictionary lookup/boxing.
        // Used whenever the source collection's item type can be determined once from its own
        // Type (the overwhelming common case: List<T>, arrays, anything implementing
        // IEnumerable<T>) rather than needing to be rediscovered per element.
        private readonly ConcurrentDictionary<(Type SourceItem, Type DestItem), Func<object, object>> _listMappers = new();

        // GetOrAdd's factory delegate closes over `this` (InvokeBuilder is an instance method),
        // so a lambda written inline at each call site would allocate a brand-new delegate
        // object on *every single call* to GetOrBuildEntryPoint/GetOrBuildListMapper — not just
        // on a cache miss — because the C# compiler can only cache a lambda as a shared static
        // delegate when it captures nothing at all; the moment it captures `this`, a fresh
        // delegate is built per evaluation unless it's stored somewhere itself. Building each
        // factory exactly once, here, in the constructor, and reusing it for the Mapper
        // instance's whole lifetime removes that per-call allocation entirely.
        private readonly Func<(Type Source, Type Destination), Func<object, object?>> _buildEntryPointFactory;
        private readonly Func<(Type Source, Type Destination), Delegate> _buildTypedEntryPointFactory;
        private readonly Func<(Type SourceItem, Type DestItem), Func<object, object>> _buildListMapperFactory;

        /// <summary>
        /// Creates a mapper backed by the given, already-populated configuration.
        /// </summary>
        public Mapper(IMapperConfig config)
        {
            _config = config;
            _buildEntryPointFactory = key => (Func<object, object?>)InvokeBuilder(
                BuildEntryPointDefinition.MakeGenericMethod(key.Source, key.Destination));
            _buildTypedEntryPointFactory = key => (Delegate)InvokeBuilder(
                BuildTypedEntryPointDefinition.MakeGenericMethod(key.Source, key.Destination));
            _buildListMapperFactory = key => (Func<object, object>)InvokeBuilder(
                BuildListMapperDefinition.MakeGenericMethod(key.SourceItem, key.DestItem));
        }

        /// <inheritdoc />
        public TDestination Map<TDestination>(object? source)
        {
            if (source is null) return default!;
            var destType = typeof(TDestination);

            if (IsGenericList(destType))
                return (TDestination)MapList(source, destType.GetGenericArguments()[0]);

            var entryPoint = GetOrBuildEntryPoint(source.GetType(), destType);
            return (TDestination)entryPoint(source)!;
        }

        /// <inheritdoc />
        public TDestination Map<TSource, TDestination>(TSource source)
        {
            // Both types are known at the call site, so this skips everything the untyped
            // overload above needs to do at runtime: no source.GetType(), no boxing of the
            // return value through object. Resolved once per (Mapper instance, type pair) via
            // the same cached-closure shape as the untyped path's _entryPoints - see
            // _typedEntryPoints above for why a plain IMapperConfig.GetMap<,>() call per Map
            // call (the pre-4.3.0 shape of this method) measured *slower* than the untyped
            // overload despite being designed to be faster. Use this overload directly (instead
            // of Map<TDestination>(object)) in any hot loop where the source type is statically
            // known.
            if (source is null) return default!;
            var compiled = (Func<TSource, TDestination>)_typedEntryPoints.GetOrAdd(
                (typeof(TSource), typeof(TDestination)), _buildTypedEntryPointFactory);
            return compiled(source);
        }

        private Func<object, object?> GetOrBuildEntryPoint(Type sourceType, Type destinationType) =>
            _entryPoints.GetOrAdd((sourceType, destinationType), _buildEntryPointFactory);

        // Called once per (Mapper instance, type pair) - via reflection, cached above - exactly
        // mirroring BuildEntryPoint except it returns the strongly-typed delegate directly
        // (as a boxed Delegate reference, cast back at the _typedEntryPoints call site) instead
        // of wrapping it in an object-parameter closure, so Map<TSource,TDestination> never
        // touches `object` at all - no source.GetType(), no cast, no boxing.
        private Delegate BuildTypedEntryPoint<TSource, TDestination>() =>
            _config.GetMap<TSource, TDestination>().GetCompiledMap(_config);

        // BuildEntryPoint (unlike the old design) resolves config.GetMap<,>() eagerly, so a
        // missing registration now throws *during* this reflective Invoke call and would
        // otherwise arrive wrapped in a TargetInvocationException — undoing the documented,
        // disclosed switch away from that wrapping (see CLAUDE.md and CHANGELOG.md). Unwrap it
        // here, once, so callers still see the real InvalidOperationException directly, with its
        // original stack trace preserved.
        private object InvokeBuilder(MethodInfo method)
        {
            try
            {
                return method.Invoke(this, null)!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable — Throw() always throws
            }
        }

        // Called once per (Mapper instance, type pair) — via reflection, cached above — to
        // resolve the builder and produce a fast, already-bound closure. TSource/TDestination
        // are ordinary C# generic parameters here, so everything inside this method, including
        // the config.GetMap<,>() call, compiles to direct calls: no further reflection. Grabs
        // the already-compiled delegate itself (GetCompiledMap), not the builder, so every
        // subsequent call through this cached closure is a single direct delegate invocation —
        // no LazyInitializer check per call. This is safe timing-wise: BuildEntryPoint only ever
        // runs in response to an actual pending Map call for this exact type pair, so compiling
        // here is no earlier than compiling on the first invocation of the closure would have
        // been. It does not eagerly compile any *other* type pair's plan (nested same-name
        // properties are still resolved lazily inside the compiled expression itself — see
        // MappingPlanCompiler), so this cannot introduce the infinite-recursion risk that eagerly
        // resolving a nested builder's own compiled delegate at plan-compile time would for a
        // self-referential type (e.g. Employee.Manager: Employee).
        private Func<object, object?> BuildEntryPoint<TSource, TDestination>()
        {
            var compiled = _config.GetMap<TSource, TDestination>().GetCompiledMap(_config);
            return source => compiled((TSource)source);
        }

        private object MapList(object source, Type destItemType)
        {
            // An empty source collection never needs to touch the type system at all (matches
            // the previous behavior, where a foreach over an empty enumerable simply never
            // triggered any per-item resolution) — this also means an empty list maps
            // successfully even if no map is registered for the (unknown) item types.
            if (source is ICollection { Count: 0 })
                return CreateInstance(typeof(List<>).MakeGenericType(destItemType));

            return TryGetDeclaredItemType(source.GetType(), out var sourceItemType)
                ? GetOrBuildListMapper(sourceItemType, destItemType)(source)
                : MapListByRuntimeType(source, destItemType);
        }

        // Resolves the declared item type of a collection from its own Type — List<T>, T[], or
        // anything implementing IEnumerable<T> — without inspecting any element. This is what
        // lets collection mapping resolve the item mapper ONCE per call (not once per item):
        // the overwhelming majority of real collections are homogeneous, so knowing the item
        // type up front is enough to run a fully strongly-typed loop.
        private static bool TryGetDeclaredItemType(Type sourceType, out Type itemType)
        {
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(List<>))
            {
                itemType = sourceType.GetGenericArguments()[0];
                return true;
            }

            if (sourceType.IsArray)
            {
                itemType = sourceType.GetElementType()!;
                return true;
            }

            foreach (var iface in sourceType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    itemType = iface.GetGenericArguments()[0];
                    return true;
                }
            }

            itemType = typeof(object);
            return false;
        }

        private Func<object, object> GetOrBuildListMapper(Type sourceItemType, Type destItemType) =>
            _listMappers.GetOrAdd((sourceItemType, destItemType), _buildListMapperFactory);

        // Builds a strongly-typed collection-mapping loop once per (Mapper instance, item type
        // pair): a real `foreach (TSourceItem item in source)` with zero per-item GetType()
        // calls, zero per-item dictionary lookups, and zero boxing of value-typed items (List<T>
        // stores T directly; only the List<TDestItem> reference itself is boxed to object once,
        // to cross back over the untyped Map<TDestination>(object) boundary). This is the fix
        // for the single biggest gap against AutoMapper/Mapster: the old implementation resolved
        // the item mapper via a boxed, dictionary-keyed lookup for every single element.
        private Func<object, object> BuildListMapper<TSourceItem, TDestItem>()
        {
            var config = _config;

            // Resolved lazily, on the first item actually mapped through this cached delegate
            // (not eagerly here) — and then reused for the remaining lifetime of this closure.
            // Deferring it preserves the "an empty collection never requires a registered map"
            // behavior for any collection that reaches this method as non-empty on this call but
            // could plausibly be empty on a future call through the same cached delegate. Caches
            // the compiled delegate itself (not the builder), so once resolved, every element in
            // every call through this closure is a single direct delegate invocation — no
            // LazyInitializer check and no extra builder.Map indirection per item, which is the
            // dominant fixed per-item cost for large collections. The benign data race on this
            // captured variable (multiple threads may each resolve and assign it independently)
            // remains safe: config.GetMap<,>().GetCompiledMap(config) is a pure, idempotent
            // lookup that always returns the exact same delegate reference.
            Func<TSourceItem, TDestItem>? compiledMap = null;

            return sourceObj =>
            {
                var sourceEnum = (IEnumerable<TSourceItem>)sourceObj;
                var result = sourceObj is ICollection<TSourceItem> sized
                    ? new List<TDestItem>(sized.Count)
                    : new List<TDestItem>();

                foreach (var item in sourceEnum)
                {
                    compiledMap ??= config.GetMap<TSourceItem, TDestItem>().GetCompiledMap(config);
                    result.Add(compiledMap(item));
                }

                return result;
            };
        }

        // Fallback for source collections whose item type can't be determined from the
        // collection's own Type alone (a non-generic IEnumerable/ArrayList, or a custom
        // collection that doesn't implement IEnumerable<T>) — dispatches per item by that item's
        // actual runtime type, same as the original implementation, but with a one-slot
        // "monomorphic inline cache": consecutive items of the same runtime type reuse the last
        // resolved entry point instead of re-querying the dictionary, so the realistic common
        // case (a homogeneous collection that merely couldn't be typed up front) still pays for
        // the dictionary lookup only once, while heterogeneous/polymorphic collections still
        // dispatch correctly per element exactly as before.
        private object MapListByRuntimeType(object source, Type destItemType)
        {
            if (source is not IEnumerable sourceEnum)
                throw new ArgumentException("Source is not enumerable.");

            var resultList = (IList)CreateInstance(typeof(List<>).MakeGenericType(destItemType));

            Type? lastItemType = null;
            Func<object, object?>? lastEntryPoint = null;

            foreach (var item in sourceEnum)
            {
                var itemType = item.GetType();
                if (itemType != lastItemType)
                {
                    lastEntryPoint = GetOrBuildEntryPoint(itemType, destItemType);
                    lastItemType = itemType;
                }
                resultList.Add(lastEntryPoint!(item)!);
            }

            return resultList;
        }

        private static bool IsGenericList(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        internal static bool TryGetListItemTypes(Type sourceType, Type destType, out Type sourceItemType, out Type destItemType)
        {
            if (IsGenericList(sourceType) && IsGenericList(destType))
            {
                sourceItemType = sourceType.GetGenericArguments()[0];
                destItemType = destType.GetGenericArguments()[0];
                return true;
            }

            sourceItemType = destItemType = typeof(object);
            return false;
        }

        internal static PropertyInfo[] GetPublicProperties(Type type) =>
            PublicPropertiesCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        internal static Dictionary<string, PropertyInfo> GetPropertiesByName(Type type) =>
            PropertiesByNameCache.GetOrAdd(type, t =>
            {
                var props = GetPublicProperties(t);
                var byName = new Dictionary<string, PropertyInfo>(props.Length);
                foreach (var p in props)
                {
                    byName[p.Name] = p; // last one wins for shadowed ("new") members
                }
                return byName;
            });

        internal static object CreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type)!;
            }
            catch (MissingMethodException ex)
            {
                throw new InvalidOperationException(
                    $"Cannot map to '{type.Name}': it has no public parameterless constructor. " +
                    "AutoMapperLite creates destination instances via a compiled constructor call, so " +
                    "positional records, readonly structs, and classes requiring constructor arguments " +
                    "are not supported as mapping destinations — use a type with a public parameterless " +
                    "constructor (init-only properties are fine, e.g. a record with { get; init; } members).",
                    ex);
            }
        }

        // Used by the compiled plan (via Expression.Call — a direct compiled call, not a
        // reflection Invoke) to convert an object-typed ForMember/ForPath result to the concrete
        // destination property type, with the same clear-exception behavior as before.
        private static TMember ConvertMappedValue<TMember>(object? value, string propertyPath)
        {
            try
            {
                return (TMember)value!;
            }
            catch (Exception ex) when (ex is InvalidCastException or NullReferenceException)
            {
                var actualType = value?.GetType().Name ?? "null";
                throw new InvalidOperationException(
                    $"The mapping configured for '{propertyPath}' produced a value of type '{actualType}' " +
                    $"that cannot be assigned to property type '{typeof(TMember).Name}'.", ex);
            }
        }

        private static void SetMappedValue(PropertyInfo prop, object destination, object? value, string propertyPath)
        {
            try
            {
                prop.SetValue(destination, value);
            }
            catch (Exception ex) when (ex is ArgumentException or TargetException)
            {
                var actualType = value?.GetType().Name ?? "null";
                throw new InvalidOperationException(
                    $"The mapping configured for '{propertyPath}' produced a value of type '{actualType}' " +
                    $"that cannot be assigned to property type '{prop.PropertyType.Name}'.", ex);
            }
        }

        // Deliberately still interpreted (reflection PropertyInfo walk), not compiled: ForPath
        // targets are typically rare relative to plain auto-mapped properties, and the dynamic,
        // arbitrary-depth segment-walking + intermediate-object-creation logic here is exactly
        // the kind of complexity not worth compiling for a cold path (see CLAUDE.md). Called from
        // the compiled plan via a direct generic method call (Expression.Call on this closed
        // MethodInfo), so the *dispatch* into this method is still fast — only the walk itself
        // remains interpreted.
        internal static void ApplyNestedMapping<TSource, TDestination>(
            object nestedInstance,
            TSource source,
            string path,
            MapBuilder<TSource, TDestination> builder)
        {
            var segments = path.Split('.');
            object current = nestedInstance;

            for (int i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                var prop = current.GetType().GetProperty(segment);
                if (prop == null) continue;

                var fullKey = string.Join(".", segments.Take(i + 1));
                if (builder.MemberMappings.TryGetValue(fullKey, out var mapFunc))
                {
                    SetMappedValue(prop, current, mapFunc(source), fullKey);
                }

                if (prop.GetValue(current) == null)
                {
                    var nextInstance = CreateInstance(prop.PropertyType);
                    prop.SetValue(current, nextInstance);
                    current = nextInstance;
                }
                else
                {
                    current = prop.GetValue(current)!;
                }
            }
        }
    }
}
