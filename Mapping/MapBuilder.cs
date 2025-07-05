using System.Linq.Expressions;

namespace AutoMapperLite.Mapping
{
    public class MapBuilder<TSource, TDestination>
    {
        internal readonly Dictionary<string, Func<TSource, object>> MemberMappings = new();

        public MapBuilder<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destMember,
            Func<TSource, object> mapFunc)
        {
            var memberName = ((MemberExpression)destMember.Body).Member.Name;
            MemberMappings[memberName] = mapFunc;
            return this;
        }

        public MapBuilder<TSource, TDestination> ForPath<TMember>(
            Expression<Func<TDestination, TMember>> destPath,
            Func<TSource, object> mapFunc)
        {
            var path = GetFullPath(destPath.Body);
            MemberMappings[path] = mapFunc;
            return this;
        }

        private string GetFullPath(Expression expr)
        {
            var pathParts = new List<string>();
            while (expr is MemberExpression member)
            {
                pathParts.Insert(0, member.Member.Name);
                expr = member.Expression ?? default!;
            }
            return string.Join(".", pathParts);
        }
    }
}