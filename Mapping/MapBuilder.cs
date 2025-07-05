using System.Linq.Expressions;

namespace AutoMapperLite
{
    public sealed class MapBuilder<TSource, TDestination>
    {
        internal Dictionary<string, Func<TSource, object?>> MemberMappings { get; } = new();

        public MapBuilder<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destination,
            Func<TSource, object?> mapFunc)
        {
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