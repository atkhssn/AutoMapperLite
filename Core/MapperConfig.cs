using AutoMapperLite.Interfaces;
using System.Collections.Concurrent;

namespace AutoMapperLite
{
    /// <summary>
    /// Default <see cref="IMapperConfig"/> implementation. Thread-safe: registering a
    /// map can safely race with lookups against an already-published (e.g. singleton) instance.
    /// </summary>
    public sealed class MapperConfig : IMapperConfig
    {
        // ConcurrentDictionary so that a config already published to a DI container
        // (registered as a singleton) tolerates a CreateMap call racing with concurrent
        // GetMap/HasMap reads from in-flight mapping calls, instead of relying entirely
        // on all profiles being configured before the config is ever read.
        private readonly ConcurrentDictionary<(Type, Type), object> _mappings = new();

        /// <inheritdoc />
        public MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var builder = new MapBuilder<TSource, TDestination>();
            _mappings[(typeof(TSource), typeof(TDestination))] = builder;
            return builder;
        }

        /// <inheritdoc />
        public MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>()
        {
            if (_mappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var builder))
            {
                return (MapBuilder<TSource, TDestination>)builder;
            }
            throw new InvalidOperationException($"Mapping not found between {typeof(TSource)} and {typeof(TDestination)}");
        }

        /// <inheritdoc />
        public bool HasMap(Type sourceType, Type destType)
        {
            return _mappings.ContainsKey((sourceType, destType));
        }

    }
}
