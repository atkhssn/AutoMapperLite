using AutoMapperLite.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AutoMapperLite.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAutoMapperLite(this IServiceCollection services, params Assembly[] assemblies)
        {
            var config = new MapperConfig();

            var profileTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(Profile).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();

            foreach (var profileType in profileTypes)
            {
                var profileInstance = (Profile)Activator.CreateInstance(profileType)!;
                profileInstance.Configure(config);
            }

            services.AddSingleton<IMapperConfig>(config);
            services.AddSingleton<IMapper, Mapper>();

            return services;
        }
    }
}