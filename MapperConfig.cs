using AutoMapperLite.Interfaces;
using System.Collections.Concurrent;

namespace AutoMapperLite
{
    public class MapperConfig : IMapperConfig
    {
        private readonly ConcurrentDictionary<(Type, Type), object> _maps = new();

        public MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var key = (typeof(TSource), typeof(TDestination));

            var builder = new MapBuilder<TSource, TDestination>();
            _maps[key] = builder;
            return builder;
        }

        public MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>()
        {
            var key = (typeof(TSource), typeof(TDestination));
            if (_maps.TryGetValue(key, out var builder))
            {
                return (MapBuilder<TSource, TDestination>)builder;
            }

            throw new InvalidOperationException($"Mapping not defined between {key.Item1.Name} and {key.Item2.Name}");
        }
    }
}