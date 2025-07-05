namespace AutoMapperLite.Mapping
{
    public class MapperConfig
    {
        internal readonly Dictionary<(Type Source, Type Destination), object> Mappings = new();

        public MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var builder = new MapBuilder<TSource, TDestination>();
            Mappings[(typeof(TSource), typeof(TDestination))] = builder;
            return builder;
        }

        public MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>()
        {
            return Mappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var map)
                ? (MapBuilder<TSource, TDestination>)map
                : throw new InvalidOperationException($"Mapping not found from {typeof(TSource)} to {typeof(TDestination)}");
        }
    }
}