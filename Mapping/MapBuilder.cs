using System.Linq.Expressions;

namespace AutoMapperLite
{
    public sealed class MapBuilder<TSource, TDestination>
    {
        internal Dictionary<string, Func<TSource, object?>> MemberMappings { get; } = new();

        // MemberMappings is only ever written during Profile.Configure(), before any
        // mapping runs, so this grouping is safe to compute once, lazily, and reuse
        // for the lifetime of the builder instead of re-scanning MemberMappings.Keys
        // with LINQ on every single object mapped.
        private readonly Lazy<Dictionary<string, List<string>>> _nestedKeysByTopLevel;

        public MapBuilder()
        {
            _nestedKeysByTopLevel = new Lazy<Dictionary<string, List<string>>>(BuildNestedKeyGroups);
        }

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
