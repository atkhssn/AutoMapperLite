using AutoMapperLite.Interfaces;
using AutoMapperLite.Mapping;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AutoMapperLite.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAutoMapperLite(this IServiceCollection services, Assembly assembly)
        {
            var config = new MapperConfig();

            var profiles = assembly.GetTypes()
                .Where(t => typeof(IProfile).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(Activator.CreateInstance)
                .Cast<IProfile>();

            foreach (var profile in profiles)
                profile.Configure(config);

            services.AddSingleton(config);
            services.AddSingleton<Mapper>();
            services.AddSingleton<IMapper>(sp => sp.GetRequiredService<Mapper>());

            return services;
        }
    }
}