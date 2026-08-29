using Xunit;

namespace AutoMapperLite.Tests
{
    public class MapperTests
    {
        [Fact]
        public void Map_ReturnsDefault_WhenSourceIsNull()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var result = mapper.Map<SimpleDestination>(null!);

            Assert.Null(result);
        }

        [Fact]
        public void Map_AutoMapsSameNameProperties()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var result = mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" });

            Assert.Equal(1, result.Id);
            Assert.Equal("test", result.Name);
        }

        [Fact]
        public void Map_UsesForMember_ForCustomMapping()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>()
                .ForMember(dest => dest.Name, src => src.Name.ToUpperInvariant());
            var mapper = new Mapper(config);

            var result = mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" });

            Assert.Equal("TEST", result.Name);
        }

        [Fact]
        public void Map_RecursesIntoRegisteredNestedMap_WhenPropertyTypesDiffer()
        {
            var config = new MapperConfig();
            config.CreateMap<Country, CountryViewModel>();
            config.CreateMap<Organization, OrganizationViewModel>()
                .ForMember(dest => dest.CountryViewModel, src => new Mapper(config).Map<CountryViewModel>(src.Country));
            var mapper = new Mapper(config);

            var result = mapper.Map<OrganizationViewModel>(
                new Organization { OrgName = "Acme", Country = new Country { Name = "Wonderland" } });

            Assert.Equal("Acme", result.OrgName);
            Assert.Equal("Wonderland", result.CountryViewModel.Name);
        }

        [Fact]
        public void Map_UsesForPath_ForDeeplyNestedDestination()
        {
            var config = new MapperConfig();
            config.CreateMap<Location, LocationViewModel>()
                .ForPath(dest => dest.OrganizationViewModel.CountryViewModel.Name,
                         src => src.Organization.Country.Name)
                .ForPath(dest => dest.OrganizationViewModel.OrgName,
                         src => src.Organization.OrgName);
            var mapper = new Mapper(config);

            var result = mapper.Map<LocationViewModel>(new Location
            {
                Address = "1 Main St",
                Organization = new Organization
                {
                    OrgName = "Acme",
                    Country = new Country { Name = "Wonderland" }
                }
            });

            Assert.Equal("1 Main St", result.Address);
            Assert.Equal("Acme", result.OrganizationViewModel.OrgName);
            Assert.Equal("Wonderland", result.OrganizationViewModel.CountryViewModel.Name);
        }

        [Fact]
        public void Map_MapsListOfObjects()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var sources = new List<SimpleSource>
            {
                new() { Id = 1, Name = "a" },
                new() { Id = 2, Name = "b" },
            };

            var result = mapper.Map<List<SimpleDestination>>(sources);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Id);
            Assert.Equal("b", result[1].Name);
        }

        [Fact]
        public void Map_SkipsMismatchedTypeProperty_WhenNoMapRegistered()
        {
            var config = new MapperConfig();
            config.CreateMap<Organization, OrganizationViewModel>();
            var mapper = new Mapper(config);

            var result = mapper.Map<OrganizationViewModel>(
                new Organization { OrgName = "Acme", Country = new Country { Name = "Wonderland" } });

            Assert.Equal("Acme", result.OrgName);
            Assert.Equal(string.Empty, result.CountryViewModel.Name);
        }

        [Fact]
        public void Map_ThrowsInvalidOperationException_WhenNoMappingRegistered()
        {
            var config = new MapperConfig();
            var mapper = new Mapper(config);

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" }));

            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }
    }
}
