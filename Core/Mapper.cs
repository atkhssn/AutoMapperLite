using AutoMapperLite.Interfaces;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace AutoMapperLite
{
    /// <summary>
    /// Default <see cref="IMapper"/> implementation. Stateless beyond the injected
    /// <see cref="IMapperConfig"/> — safe to use concurrently and safe to register
    /// as a singleton.
    /// </summary>
    public sealed class Mapper : IMapper
    {
        private static readonly MethodInfo MapSingleDefinition =
            typeof(Mapper).GetMethod(nameof(MapSingle), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo MapDefinition =
            typeof(Mapper).GetMethod(nameof(Map), BindingFlags.Public | BindingFlags.Instance)!;

        private static readonly ConcurrentDictionary<(Type Source, Type Destination), MethodInfo> MapSingleMethodCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> MapMethodCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PublicPropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertiesByNameCache = new();

        private readonly IMapperConfig _config;

        /// <summary>
        /// Creates a mapper backed by the given, already-populated configuration.
        /// </summary>
        public Mapper(IMapperConfig config)
        {
            _config = config;
        }

        /// <inheritdoc />
        public TDestination Map<TDestination>(object? source)
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
                    var mapMethod = GetMapSingleMethod(item.GetType(), itemType);
                    resultList.Add(mapMethod.Invoke(this, new object[] { item })!);
                }
                return (TDestination)resultList;
            }

            return MapSingleInternal<TDestination>(source);
        }

        private TDestination MapSingleInternal<TDestination>(object source)
        {
            var method = GetMapSingleMethod(source.GetType(), typeof(TDestination));
            return (TDestination)method.Invoke(this, new object[] { source })!;
        }

        private static bool IsGenericList(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        private static bool TryGetListItemTypes(Type sourceType, Type destType, out Type sourceItemType, out Type destItemType)
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

        private static MethodInfo GetMapSingleMethod(Type sourceType, Type destinationType) =>
            MapSingleMethodCache.GetOrAdd((sourceType, destinationType),
                key => MapSingleDefinition.MakeGenericMethod(key.Source, key.Destination));

        private static MethodInfo GetMapMethod(Type destinationType) =>
            MapMethodCache.GetOrAdd(destinationType, t => MapDefinition.MakeGenericMethod(t));

        private static PropertyInfo[] GetPublicProperties(Type type) =>
            PublicPropertiesCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        private static Dictionary<string, PropertyInfo> GetPropertiesByName(Type type) =>
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

        private TDestination MapSingle<TSource, TDestination>(TSource source)
        {
            var builder = _config.GetMap<TSource, TDestination>();
            var destinationType = typeof(TDestination);

            // Boxed once and mutated in place for the rest of this method — if TDestination
            // is a value type, calling PropertyInfo.SetValue against a freshly-boxed copy on
            // every call (as opposed to this single shared box) would silently discard every
            // assignment once unboxed back into a TDestination local.
            object destination = CreateInstance(destinationType);

            var destProps = GetPublicProperties(destinationType);
            var sourcePropsByName = GetPropertiesByName(typeof(TSource));

            foreach (var prop in destProps)
            {
                var path = prop.Name;

                // 1. Handle ForMember / ForPath style mapping
                if (builder.MemberMappings.TryGetValue(path, out var customMap))
                {
                    SetMappedValue(prop, destination, customMap(source), path);
                    continue;
                }

                // 2. Handle nested object mapping (ForPath style)
                var nestedKeys = builder.GetNestedKeys(path);
                if (nestedKeys.Count > 0)
                {
                    var nestedInstance = CreateInstance(prop.PropertyType);
                    for (var i = 0; i < nestedKeys.Count; i++)
                    {
                        ApplyNestedMapping(nestedInstance, source, nestedKeys[i], builder);
                    }
                    prop.SetValue(destination, nestedInstance);
                    continue;
                }

                // 3. Auto-map same-name properties
                if (!sourcePropsByName.TryGetValue(prop.Name, out var sourceProp) || !sourceProp.CanRead || !prop.CanWrite)
                    continue;

                var value = sourceProp.GetValue(source);
                if (value == null) continue;

                if (prop.PropertyType == sourceProp.PropertyType)
                {
                    prop.SetValue(destination, value);
                    continue;
                }

                // Type mismatch: only map if a matching type map is registered, either
                // directly, or (for List<T> properties) between the two item types.
                var canMap = _config.HasMap(sourceProp.PropertyType, prop.PropertyType)
                    || (TryGetListItemTypes(sourceProp.PropertyType, prop.PropertyType, out var itemSourceType, out var itemDestType)
                        && _config.HasMap(itemSourceType, itemDestType));

                if (canMap)
                {
                    var mapMethod = GetMapMethod(prop.PropertyType);
                    var mappedValue = mapMethod.Invoke(this, new object[] { value });
                    prop.SetValue(destination, mappedValue);
                }
                // else: no registered map for this type pair — skip invalid assignment
            }

            return (TDestination)destination;
        }

        private static object CreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type)!;
            }
            catch (MissingMethodException ex)
            {
                throw new InvalidOperationException(
                    $"Cannot map to '{type.Name}': it has no public parameterless constructor. " +
                    "AutoMapperLite creates destination instances via Activator.CreateInstance, so " +
                    "positional records, readonly structs, and classes requiring constructor arguments " +
                    "are not supported as mapping destinations — use a type with a public parameterless " +
                    "constructor (init-only properties are fine, e.g. a record with { get; init; } members).",
                    ex);
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

        private static void ApplyNestedMapping<TSource, TDestination>(
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
