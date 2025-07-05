namespace AutoMapperLite.Mapping;

public class MapperConfig
{
    private readonly Dictionary<(Type source, Type destination), object> _maps = new();

    public MapConfig<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var map = new MapConfig<TSource, TDestination>();
        _maps[(typeof(TSource), typeof(TDestination))] = map;
        return map;
    }

    internal bool TryMap(Type sourceType, Type destType, object sourceValue, out object? result)
    {
        if (_maps.TryGetValue((sourceType, destType), out var mapObj))
        {
            var method = mapObj.GetType().GetMethod("Map", new[] { sourceType, typeof(MapperConfig) });
            result = method?.Invoke(mapObj, new[] { sourceValue, this });
            return result != null;
        }

        result = null;
        return false;
    }

    internal MapConfig<TSource, TDestination>? GetConfig<TSource, TDestination>()
    {
        return _maps.TryGetValue((typeof(TSource), typeof(TDestination)), out var config)
            ? config as MapConfig<TSource, TDestination>
            : null;
    }
}