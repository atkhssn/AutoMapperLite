using AutoMapperLite;
using AutoMapperLite.Interfaces;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
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