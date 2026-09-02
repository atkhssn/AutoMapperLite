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

        /// <summary>
        /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>,
        /// using the map registered for the exact (<typeparamref name="TSource"/>,
        /// <typeparamref name="TDestination"/>) pair.
        /// </summary>
        /// <remarks>
        /// Prefer this overload over <see cref="Map{TDestination}(object?)"/> whenever the source
        /// type is known at the call site: it skips resolving <c>source.GetType()</c> and the
        /// internal type-pair cache lookup that <see cref="Map{TDestination}(object?)"/> needs to
        /// support an untyped <see cref="object"/> source, so it is the fastest way to call
        /// AutoMapperLite. Note the difference in dispatch: <see cref="Map{TDestination}(object?)"/>
        /// resolves the map to use from <c>source</c>'s <em>runtime</em> type, while this overload
        /// always uses the <em>compile-time</em> type <typeparamref name="TSource"/> — if you pass
        /// a variable declared as a base type but holding a derived instance, this overload maps
        /// using the base type's registration, not the derived type's.
        /// </remarks>
        /// <typeparam name="TSource">The compile-time type of <paramref name="source"/>.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">
        /// The source object to map from. If <see langword="null"/>, returns
        /// <see langword="default"/>(<typeparamref name="TDestination"/>).
        /// </param>
        /// <returns>A new, populated instance of <typeparamref name="TDestination"/>.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// No map was registered for the (<typeparamref name="TSource"/>, <typeparamref name="TDestination"/>) pair.
        /// </exception>
        TDestination Map<TSource, TDestination>(TSource source);
    }
}
