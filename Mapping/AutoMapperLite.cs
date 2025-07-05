namespace AutoMapperLite.Mapping;

public class AutoMapperLite : IMapper
{
    private readonly MapperConfig _config;

    public AutoMapperLite(MapperConfig config)
    {
        _config = config;
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        var mapConfig = _config.GetConfig<TSource, TDestination>()
                        ?? throw new InvalidOperationException($"Mapping not found from {typeof(TSource)} to {typeof(TDestination)}");

        return mapConfig.Map(source, _config);
    }
}