namespace AutoMapperLite.Mapping;

public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);
}