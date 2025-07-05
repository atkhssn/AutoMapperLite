using System.Linq.Expressions;

namespace AutoMapperLite
{
    public class MapBuilder<TSource, TDestination>
    {
        public Dictionary<string, Func<TSource, object?>> MemberMappings { get; } = new();

        public MapBuilder<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destSelector,
            Func<TSource, object?> mapFunc)
        {
            var memberName = GetMemberName(destSelector);
            MemberMappings[memberName] = mapFunc;
            return this;
        }

        public MapBuilder<TSource, TDestination> ForPath(string path, Func<TSource, object?> mapFunc)
        {
            MemberMappings[path] = mapFunc;
            return this;
        }

        private string GetMemberName<TMember>(Expression<Func<TDestination, TMember>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression operand)
                return operand.Member.Name;

            throw new InvalidOperationException("Invalid expression");
        }
    }
}
