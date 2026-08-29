using AutoMapperLite;
using AutoMapperLite.Interfaces;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Dependency-injection registration for AutoMapperLite.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Scans <paramref name="assembly"/> for concrete <see cref="Profile"/> subclasses with a
        /// public parameterless constructor, runs each one's <see cref="Profile.Configure"/> against
        /// a shared <see cref="MapperConfig"/>, then registers <see cref="IMapperConfig"/> and
        /// <see cref="IMapper"/> as singletons.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
        public static IServiceCollection AddAutoMapperLite(this IServiceCollection services, Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var config = new MapperConfig();

            var profileTypes = assembly.GetTypes()
                .Where(t => typeof(Profile).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);

            foreach (var profileType in profileTypes)
            {
                var profile = (Profile)Activator.CreateInstance(profileType)!;
                profile.Configure(config);
            }

            services.AddSingleton<IMapperConfig>(config);

            // Mapper holds no per-request state beyond the (already-singleton) config,
            // so it is safe and allocation-free to register it as a singleton too.
            services.AddSingleton<IMapper, Mapper>();

            return services;
        }
    }
}