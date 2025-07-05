using AutoMapperLite.Interfaces;
using System.Collections;
using System.Reflection;

namespace AutoMapperLite
{
    public class Mapper : IMapper
    {
        private readonly IMapperConfig _config;

        public Mapper(IMapperConfig config)
        {
            _config = config;
        }

        public TDestination Map<TDestination>(object source)
        {
            if (source == null) return default!;

            var destType = typeof(TDestination);

            if (IsGenericList(destType))
            {
                var itemType = destType.GetGenericArguments()[0];

                if (source is not IEnumerable sourceEnumerable)
                    throw new ArgumentException("Source is not enumerable but destination is a collection");

                var listType = typeof(List<>).MakeGenericType(itemType);
                var resultList = (IList)Activator.CreateInstance(listType)!;

                foreach (var item in sourceEnumerable)
                {
                    var mapMethod = typeof(Mapper).GetMethod(nameof(MapSingle), BindingFlags.NonPublic | BindingFlags.Instance)!;
                    var genericMapMethod = mapMethod.MakeGenericMethod(item.GetType(), itemType);
                    var mappedItem = genericMapMethod.Invoke(this, new[] { item });
                    resultList.Add(mappedItem);
                }

                return (TDestination)resultList;
            }
            else
            {
                var sourceType = source.GetType();
                var method = typeof(Mapper).GetMethod(nameof(MapSingle), BindingFlags.NonPublic | BindingFlags.Instance)!;
                var genericMethod = method.MakeGenericMethod(sourceType, destType);
                var result = genericMethod.Invoke(this, new[] { source });
                return (TDestination)result!;
            }
        }

        private bool IsGenericList(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        private TDestination MapSingle<TSource, TDestination>(TSource source)
        {
            var builder = _config.GetMap<TSource, TDestination>();
            var destination = Activator.CreateInstance<TDestination>()!;

            foreach (var prop in typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propPath = prop.Name;

                // Custom member mapping
                if (builder.MemberMappings.TryGetValue(propPath, out var customMap))
                {
                    prop.SetValue(destination, customMap(source));
                    continue;
                }

                // Nested path mapping
                var nestedKey = builder.MemberMappings.Keys.FirstOrDefault(k => k.StartsWith(propPath + "."));
                if (nestedKey != null)
                {
                    var nestedObj = Activator.CreateInstance(prop.PropertyType)!;
                    ApplyNestedMapping<TSource, TDestination>(nestedObj, source, nestedKey, builder);
                    prop.SetValue(destination, nestedObj);
                    continue;
                }

                // Auto-map same-name
                var sourceProp = typeof(TSource).GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead && prop.CanWrite)
                {
                    var value = sourceProp.GetValue(source);
                    prop.SetValue(destination, value);
                }
            }

            return destination;
        }

        private void ApplyNestedMapping<TSource, TDestination>(object nestedObj, TSource source, string path, MapBuilder<TSource, TDestination> builder)
        {
            var segments = path.Split('.');
            object currentObj = nestedObj;

            for (int i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                var prop = currentObj?.GetType().GetProperty(segment);
                if (prop == null) continue;

                var key = string.Join('.', segments.Take(i + 1));
                if (builder.MemberMappings.TryGetValue(key, out var customMap))
                {
                    prop.SetValue(currentObj, customMap(source));
                }

                var subObj = prop.GetValue(currentObj);
                if (subObj == null)
                {
                    subObj = Activator.CreateInstance(prop.PropertyType);
                    prop.SetValue(currentObj, subObj);
                }

                currentObj = subObj ?? default!;
            }
        }
    }
}