namespace AutoMapperLite.Interfaces
{
    public interface IMapperConfig
    {
        MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>();
        MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>();
    }
}