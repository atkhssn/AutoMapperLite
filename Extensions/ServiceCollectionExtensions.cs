using AutoMapperLite.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace AutoMapperLite.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAutoMapperLite(this IServiceCollection services, Action<MapperConfig> configure)
    {
        var config = new MapperConfig();
        configure(config);
        services.AddSingleton(config);
        services.AddSingleton<IMapper, Mapping.AutoMapperLite>();
        return services;
    }
}