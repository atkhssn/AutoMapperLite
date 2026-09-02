using System.Linq.Expressions;
using System.Threading;

namespace AutoMapperLite
{
    /// <summary>
    /// Fluent builder for a single source/destination type mapping, returned by
    /// <see cref="AutoMapperLite.Interfaces.IMapperConfig.CreateMap{TSource, TDestination}"/>.
    /// </summary>
    public sealed class MapBuilder<TSource, TDestination>
    {
        internal Dictionary<string, Func<TSource, object?>> MemberMappings { get; } = new();

        // MemberMappings is only ever written during Profile.Configure(), before any
        // mapping runs, so this grouping is safe to compute once, lazily, and reuse
        // for the lifetime of the builder instead of re-scanning MemberMappings.Keys
        // with LINQ on every single object mapped.
        private readonly Lazy<Dictionary<string, List<string>>> _nestedKeysByTopLevel;

        // Compiled once, lazily, from this builder's own MemberMappings/nested-path
        // configuration — see MappingPlanCompiler. Scoped to this specific builder instance
        // (not a global cache keyed by type pair alone), so two different IMapperConfig
        // instances mapping the same (TSource, TDestination) pair differently never collide.
        private Func<TSource, TDestination>? _compiledMap;

        /// <summary>Creates an empty builder. Normally obtained via <c>CreateMap</c>, not constructed directly.</summary>
        public MapBuilder()
        {
            _nestedKeysByTopLevel = new Lazy<Dictionary<string, List<string>>>(BuildNestedKeyGroups);
        }

        internal TDestination Map(TSource source, AutoMapperLite.Interfaces.IMapperConfig config) =>
            GetCompiledMap(config)(source);

        // Exposes the compiled delegate itself (rather than invoking it) so a caller that will
        // map many values through the same builder - a collection loop, or a cached dispatch
        // entry point - can hold/reuse the raw Func directly instead of calling back in here on
        // every single Map call.
        //
        // The Volatile.Read fast path below is not just a style choice: a naive
        // `LazyInitializer.EnsureInitialized(ref _compiledMap, () => MappingPlanCompiler.Compile(this, config))`
        // written directly in this method's body allocates a NEW closure (capturing `this` and
        // `config`) AND a new delegate wrapping it on every single call, even once _compiledMap
        // is already set - the C# compiler must materialize that lambda expression as an actual
        // object at the call site before EnsureInitialized ever gets a chance to decide whether
        // to invoke it. Every call to this method paid for that throwaway allocation, including
        // the warm/steady-state path where it's never used - this was quietly inflating
        // allocations for both IMapper.Map<TSource,TDestination> and, per-parent-call, for every
        // same-name nested single-object property compiled via MappingPlanCompiler's
        // BuildNestedObjectAssignment. Checking the field directly first and only falling
        // through to the lambda-allocating slow path once, ever, per builder, removes it.
        internal Func<TSource, TDestination> GetCompiledMap(AutoMapperLite.Interfaces.IMapperConfig config) =>
            Volatile.Read(ref _compiledMap) ?? CompileAndCache(config);

        private Func<TSource, TDestination> CompileAndCache(AutoMapperLite.Interfaces.IMapperConfig config) =>
            LazyInitializer.EnsureInitialized(ref _compiledMap, () => MappingPlanCompiler.Compile(this, config));

        /// <summary>
        /// Configures mapping for a single, top-level destination property.
        /// </summary>
        /// <param name="destination">Expression selecting a destination property, e.g. <c>dest =&gt; dest.Name</c>.</param>
        /// <param name="mapFunc">
        /// Function producing the value to assign. Must return a value already assignable
        /// to the destination property's type — if the source and destination property
        /// types differ, map through an <see cref="AutoMapperLite.Interfaces.IMapper"/>
        /// inside this function rather than returning the raw source value.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="mapFunc"/> is <see langword="null"/>.</exception>
        public MapBuilder<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destination,
            Func<TSource, object?> mapFunc)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(mapFunc);

            var path = GetMemberPath(destination.Body);
            MemberMappings[path] = mapFunc;
            return this;
        }

        /// <summary>
        /// Configures mapping for a destination property several levels deep, e.g.
        /// <c>dest =&gt; dest.A.B.C</c>. Functionally an alias of <see cref="ForMember{TMember}"/> —
        /// intermediate objects along the path are instantiated automatically as needed,
        /// and multiple <see cref="ForPath{TMember}"/> calls under the same top-level
        /// destination property are all applied to the same nested instance.
        /// </summary>
        /// <param name="destination">Expression selecting a nested destination path.</param>
        /// <param name="mapFunc">Function producing the value to assign at that path; same rules as <see cref="ForMember{TMember}"/>.</param>
        public MapBuilder<TSource, TDestination> ForPath<TMember>(
            Expression<Func<TDestination, TMember>> destination,
            Func<TSource, object?> mapFunc)
        {
            return ForMember(destination, mapFunc);
        }

        /// <summary>
        /// Registered ForPath keys nested under <paramref name="topLevelProperty"/>
        /// (e.g. "A.B" and "A.C.D" for top-level property "A").
        /// </summary>
        internal IReadOnlyList<string> GetNestedKeys(string topLevelProperty) =>
            _nestedKeysByTopLevel.Value.TryGetValue(topLevelProperty, out var keys)
                ? keys
                : Array.Empty<string>();

        private Dictionary<string, List<string>> BuildNestedKeyGroups()
        {
            var groups = new Dictionary<string, List<string>>();
            foreach (var key in MemberMappings.Keys)
            {
                var dotIndex = key.IndexOf('.', StringComparison.Ordinal);
                if (dotIndex < 0) continue;

                var topLevel = key[..dotIndex];
                if (!groups.TryGetValue(topLevel, out var list))
                {
                    list = new List<string>();
                    groups[topLevel] = list;
                }
                list.Add(key);
            }
            return groups;
        }

        private static string GetMemberPath(Expression expression)
        {
            var path = new List<string>();
            while (expression is MemberExpression memberExpr)
            {
                path.Insert(0, memberExpr.Member.Name);
                expression = memberExpr.Expression!;
            }
            return string.Join(".", path);
        }
    }
}
