using AutoMapperLite.Interfaces;

namespace AutoMapperLite
{
    public sealed class MapperConfig : IMapperConfig
    {
        private readonly Dictionary<(Type, Type), object> _mappings = new();

        public MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var builder = new MapBuilder<TSource, TDestination>();
            _mappings[(typeof(TSource), typeof(TDestination))] = builder;
            return builder;
        }

        public MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>()
        {
            if (_mappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var builder))
            {
                return (MapBuilder<TSource, TDestination>)builder;
            }
            throw new InvalidOperationException($"Mapping not found between {typeof(TSource)} and {typeof(TDestination)}");
        }

        public bool HasMap(Type sourceType, Type destType)
        {
            return _mappings.ContainsKey((sourceType, destType));
        }

    }
}