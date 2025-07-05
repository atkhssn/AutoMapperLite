namespace AutoMapperLite.Interfaces
{
    public interface IMapperConfig
    {
        MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>();
        MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>();
        bool HasMap(Type sourceType, Type destType);
    }
}