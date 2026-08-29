using Xunit;

namespace AutoMapperLite.Tests
{
    public class MapperConfigTests
    {
        [Fact]
        public void CreateMap_RegistersMapping()
        {
            var config = new MapperConfig();

            config.CreateMap<SimpleSource, SimpleDestination>();

            Assert.True(config.HasMap(typeof(SimpleSource), typeof(SimpleDestination)));
        }

        [Fact]
        public void HasMap_ReturnsFalse_WhenNotRegistered()
        {
            var config = new MapperConfig();

            Assert.False(config.HasMap(typeof(SimpleSource), typeof(SimpleDestination)));
        }

        [Fact]
        public void GetMap_ReturnsRegisteredBuilder()
        {
            var config = new MapperConfig();
            var created = config.CreateMap<SimpleSource, SimpleDestination>();

            var retrieved = config.GetMap<SimpleSource, SimpleDestination>();

            Assert.Same(created, retrieved);
        }

        [Fact]
        public void GetMap_Throws_WhenNotRegistered()
        {
            var config = new MapperConfig();

            Assert.Throws<InvalidOperationException>(
                () => config.GetMap<SimpleSource, SimpleDestination>());
        }

        [Fact]
        public void CreateMap_IsThreadSafe_UnderConcurrentRegistration()
        {
            var config = new MapperConfig();

            Parallel.For(0, 500, i =>
            {
                config.CreateMap<SimpleSource, SimpleDestination>();
                config.HasMap(typeof(SimpleSource), typeof(SimpleDestination));
            });

            Assert.True(config.HasMap(typeof(SimpleSource), typeof(SimpleDestination)));
        }
    }
}
