using AutoMapperLite.Interfaces;
using System.Collections;
using System.Reflection;

namespace AutoMapperLite
{
    public sealed class Mapper : IMapper
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
                if (source is not IEnumerable sourceEnum)
                    throw new ArgumentException("Source is not enumerable.");

                var resultList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                foreach (var item in sourceEnum)
                {
                    var mapMethod = GetType().GetMethod(nameof(MapSingle), BindingFlags.NonPublic | BindingFlags.Instance)!
                        .MakeGenericMethod(item.GetType(), itemType);
                    resultList.Add(mapMethod.Invoke(this, new[] { item })!);
                }
                return (TDestination)resultList;
            }

            return MapSingleInternal<TDestination>(source);
        }

        private TDestination MapSingleInternal<TDestination>(object source)
        {
            var method = GetType().GetMethod(nameof(MapSingle), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(source.GetType(), typeof(TDestination));
            return (TDestination)method.Invoke(this, new[] { source })!;
        }

        private bool IsGenericList(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        private TDestination MapSingle<TSource, TDestination>(TSource source)
        {
            var builder = _config.GetMap<TSource, TDestination>();
            var destination = Activator.CreateInstance<TDestination>()!;
            var destProps = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in destProps)
            {
                var path = prop.Name;

                // 1. Handle ForMember / ForPath style mapping
                if (builder.MemberMappings.TryGetValue(path, out var customMap))
                {
                    prop.SetValue(destination, customMap(source));
                    continue;
                }

                // 2. Handle nested object mapping (ForPath style)
                var nestedKey = builder.MemberMappings.Keys.FirstOrDefault(k => k.StartsWith(path + "."));
                if (nestedKey != null)
                {
                    var nestedInstance = Activator.CreateInstance(prop.PropertyType)!;
                    ApplyNestedMapping(nestedInstance, source, nestedKey, builder);
                    prop.SetValue(destination, nestedInstance);
                    continue;
                }

                // 3. Auto-map same-name properties
                var sourceProp = typeof(TSource).GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead && prop.CanWrite)
                {
                    var value = sourceProp.GetValue(source);
                    if (value == null) continue;

                    // Check if type needs mapping
                    if (prop.PropertyType != sourceProp.PropertyType)
                    {
                        if (_config.HasMap(sourceProp.PropertyType, prop.PropertyType))
                        {
                            var mapMethod = typeof(Mapper)
                                .GetMethod(nameof(Map), BindingFlags.Public | BindingFlags.Instance)!
                                .MakeGenericMethod(prop.PropertyType);
                            var mappedValue = mapMethod.Invoke(this, new[] { value });
                            prop.SetValue(destination, mappedValue);
                        }
                        else
                        {
                            // Skip invalid assignment
                            continue;
                        }
                    }
                    else
                    {
                        // Direct assignment if types match Atik
                        prop.SetValue(destination, value);
                    }
                }
            }

            return destination;
        }

        private void ApplyNestedMapping<TSource, TDestination>(
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
                    prop.SetValue(current, mapFunc(source));
                }

                if (prop.GetValue(current) == null)
                {
                    var nextInstance = Activator.CreateInstance(prop.PropertyType)!;
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