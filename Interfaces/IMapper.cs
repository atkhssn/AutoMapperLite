namespace AutoMapperLite.Interfaces
{
    public interface IMapper
    {
        TDestination Map<TDestination>(object source);
    }
}