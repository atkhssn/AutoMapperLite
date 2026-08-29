using System.Reflection;
using AutoMapperLite.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapperLite.Tests
{
    public class SimpleTestProfile : Profile
    {
        public override void Configure(IMapperConfig config)
        {
            config.CreateMap<SimpleSource, SimpleDestination>();
        }
    }

    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddAutoMapperLite_RegistersMapperAndConfig()
        {
            var services = new ServiceCollection();

            services.AddAutoMapperLite(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IMapperConfig>());
            Assert.NotNull(provider.GetService<IMapper>());
        }

        [Fact]
        public void AddAutoMapperLite_AppliesDiscoveredProfiles()
        {
            var services = new ServiceCollection();
            services.AddAutoMapperLite(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            var result = mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" });

            Assert.Equal(1, result.Id);
            Assert.Equal("test", result.Name);
        }
    }
}
