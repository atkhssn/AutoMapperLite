using AutoMapperLite.Interfaces;

namespace AutoMapperLite
{
    /// <summary>
    /// Base class for mapping profiles. Subclass this, override <see cref="Configure"/>,
    /// and give the subclass a public parameterless constructor so it can be discovered
    /// by <c>AddAutoMapperLite</c>.
    /// </summary>
    public abstract class Profile
    {
        /// <summary>
        /// Register <c>CreateMap</c> calls (and any <c>ForMember</c>/<c>ForPath</c>
        /// configuration) against <paramref name="config"/>.
        /// </summary>
        public abstract void Configure(IMapperConfig config);
    }
}
