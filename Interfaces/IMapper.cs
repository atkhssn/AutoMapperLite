namespace AutoMapperLite.Interfaces
{
    /// <summary>
    /// Maps a source object to a new instance of a destination type, using the
    /// mappings registered against the injected <see cref="IMapperConfig"/>.
    /// </summary>
    public interface IMapper
    {
        /// <summary>
        /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>.
        /// </summary>
        /// <typeparam name="TDestination">
        /// The destination type. If this is <see cref="List{T}"/>, <paramref name="source"/>
        /// must implement <see cref="System.Collections.IEnumerable"/> and each element is
        /// mapped individually into the resulting list.
        /// </typeparam>
        /// <param name="source">
        /// The source object to map from. If <see langword="null"/>, returns
        /// <see langword="default"/>(<typeparamref name="TDestination"/>).
        /// </param>
        /// <returns>A new, populated instance of <typeparamref name="TDestination"/>.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// No map was registered for the (source type, <typeparamref name="TDestination"/>) pair.
        /// </exception>
        TDestination Map<TDestination>(object? source);
    }
}
