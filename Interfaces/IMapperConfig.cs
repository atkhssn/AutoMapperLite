namespace AutoMapperLite.Interfaces
{
    /// <summary>
    /// Registry of type-to-type mappings, populated inside <see cref="Profile.Configure"/>
    /// and consumed by <see cref="IMapper"/> at mapping time. Safe for concurrent use.
    /// </summary>
    public interface IMapperConfig
    {
        /// <summary>
        /// Registers a mapping between <typeparamref name="TSource"/> and
        /// <typeparamref name="TDestination"/>, returning a fluent builder for
        /// further configuration via <see cref="MapBuilder{TSource, TDestination}.ForMember{TMember}"/>
        /// and <see cref="MapBuilder{TSource, TDestination}.ForPath{TMember}"/>.
        /// </summary>
        MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>();

        /// <summary>
        /// Retrieves the builder previously registered via <see cref="CreateMap{TSource, TDestination}"/>.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">No map is registered for this type pair.</exception>
        MapBuilder<TSource, TDestination> GetMap<TSource, TDestination>();

        /// <summary>
        /// Returns whether a map is registered for the given source/destination type pair.
        /// </summary>
        bool HasMap(Type sourceType, Type destType);
    }
}
