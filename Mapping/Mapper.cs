using AutoMapperLite.Interfaces;
using System.Reflection;

namespace AutoMapperLite.Mapping
{
    public class Mapper : IMapper
    {
        private readonly MapperConfig _config;

        public Mapper(MapperConfig config) => _config = config;

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            if (source == null) return default!;

            var builder = _config.GetMap<TSource, TDestination>();
            var destination = Activator.CreateInstance<TDestination>();

            foreach (var prop in typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propPath = prop.Name;

                // Check for custom mapping
                if (builder.MemberMappings.TryGetValue(propPath, out var customMap))
                {
                    prop.SetValue(destination, customMap(source));
                    continue;
                }

                // Check for nested path mapping
                var matchingPath = builder.MemberMappings.Keys.FirstOrDefault(k => k.StartsWith(propPath + "."));
                if (matchingPath != null)
                {
                    var nestedObj = Activator.CreateInstance(prop.PropertyType);
                    ApplyNestedMapping(nestedObj ?? default!, source, matchingPath, builder);
                    prop.SetValue(destination, nestedObj);
                    continue;
                }

                // Fallback: same-name auto copy
                var sourceProp = typeof(TSource).GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead && prop.CanWrite)
                    prop.SetValue(destination, sourceProp.GetValue(source));
            }

            return destination;
        }

        private void ApplyNestedMapping(object target, object source, string pathPrefix, object builderObj)
        {
            var builder = (dynamic)builderObj;
            foreach (var map in builder.MemberMappings)
            {
                if (!map.Key.StartsWith(pathPrefix)) continue;

                var nestedPropPath = map.Key.Substring(pathPrefix.Length + 1);
                var nestedProp = target.GetType().GetProperty(nestedPropPath);
                if (nestedProp != null)
                    nestedProp.SetValue(target, map.Value((dynamic)source));
            }
        }
    }
}