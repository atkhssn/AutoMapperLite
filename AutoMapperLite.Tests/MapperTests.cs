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
        public void Map_ForPath_CanAssignAnEntireMappedNestedObject()
        {
            // Regression test for the README's own "Create Mapping Profiles" example:
            // it used to pass the raw source sub-object straight through a ForPath
            // function targeting a differently-typed destination property, which threw
            // at runtime. The function must return an already-mapped value instead.
            var config = new MapperConfig();
            config.CreateMap<Country, CountryViewModel>();
            var recursiveMapper = new Mapper(config);
            config.CreateMap<Location, LocationViewModel>()
                .ForPath(dest => dest.OrganizationViewModel.CountryViewModel,
                         src => recursiveMapper.Map<CountryViewModel>(src.Organization.Country));
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

            Assert.Equal("Wonderland", result.OrganizationViewModel.CountryViewModel.Name);
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

        [Fact]
        public void Map_AutoMapsNestedListProperty_WhenItemTypesHaveRegisteredMap()
        {
            // Regression test: previously, a List<TSource> destination property was
            // only auto-mapped if HasMap(List<TSource>, List<TDest>) was registered
            // directly, which nobody ever does (only the item types are registered).
            // Nested list properties were silently skipped as a result.
            var config = new MapperConfig();
            config.CreateMap<Employee, EmployeeViewModel>();
            config.CreateMap<Department, DepartmentViewModel>();
            var mapper = new Mapper(config);

            var result = mapper.Map<DepartmentViewModel>(new Department
            {
                DeptName = "Engineering",
                Employees = new List<Employee>
                {
                    new() { Name = "Ada" },
                    new() { Name = "Alan" },
                }
            });

            Assert.Equal("Engineering", result.DeptName);
            Assert.Equal(2, result.Employees.Count);
            Assert.Equal("Ada", result.Employees[0].Name);
            Assert.Equal("Alan", result.Employees[1].Name);
        }

        [Fact]
        public void Map_PopulatesValueTypeDestination()
        {
            // Regression test: MapSingle used to hold `destination` as a TDestination
            // local. For a struct TDestination, each PropertyInfo.SetValue call boxed
            // a fresh temporary copy and mutated that instead of the real local,
            // silently discarding every property assignment.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, PointStructDto>()
                .ForMember(dest => dest.X, src => src.Id)
                .ForMember(dest => dest.Y, src => src.Id * 2);
            var mapper = new Mapper(config);

            var result = mapper.Map<PointStructDto>(new SimpleSource { Id = 5, Name = "n/a" });

            Assert.Equal(5, result.X);
            Assert.Equal(10, result.Y);
        }

        [Fact]
        public void Map_ThrowsDescriptiveException_WhenForMemberReturnsIncompatibleValue()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>()
                .ForMember(dest => dest.Id, src => "not an int");
            var mapper = new Mapper(config);

            // MapSingle runs behind a reflection Invoke() call, so the exception it
            // throws arrives wrapped in a TargetInvocationException (same as the
            // "no mapping registered" case above).
            var outer = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" }));

            var inner = Assert.IsType<InvalidOperationException>(outer.InnerException);
            Assert.Contains("Id", inner.Message);
        }

        [Fact]
        public void Map_IsThreadSafe_UnderConcurrentUse()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            config.CreateMap<Country, CountryViewModel>();
            config.CreateMap<Organization, OrganizationViewModel>();
            var mapper = new Mapper(config);

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            Parallel.For(0, 2000, i =>
            {
                try
                {
                    var simple = mapper.Map<SimpleDestination>(new SimpleSource { Id = i, Name = $"n{i}" });
                    if (simple.Id != i) throw new Exception("Simple map mismatch");

                    var org = mapper.Map<OrganizationViewModel>(new Organization
                    {
                        OrgName = $"org{i}",
                        Country = new Country { Name = "X" }
                    });
                    if (org.OrgName != $"org{i}") throw new Exception("Org map mismatch");

                    var list = mapper.Map<List<SimpleDestination>>(new List<SimpleSource>
                    {
                        new() { Id = i, Name = "a" },
                        new() { Id = i + 1, Name = "b" },
                    });
                    if (list.Count != 2) throw new Exception("List map mismatch");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        [Fact]
        public void Map_AutoMapsSameEnumType()
        {
            var config = new MapperConfig();
            config.CreateMap<Order, OrderDto>();
            var mapper = new Mapper(config);

            var result = mapper.Map<OrderDto>(new Order { Status = OrderStatus.Shipped });

            Assert.Equal(OrderStatus.Shipped, result.Status);
        }

        [Fact]
        public void Map_SkipsAssignment_WhenSourcePropertyIsNull()
        {
            // A null source property does not overwrite an existing destination default —
            // it is skipped entirely, not assigned as null. A same-type round trip can't
            // distinguish "skipped" from "assigned null" since both look like null; this
            // uses a destination with a non-null default to make the distinction observable.
            var config = new MapperConfig();
            config.CreateMap<NullSource, DefaultedDestination>();
            var mapper = new Mapper(config);

            var result = mapper.Map<DefaultedDestination>(new NullSource { Text = null });

            Assert.Equal("server-default", result.Text);
        }

        [Fact]
        public void Map_PopulatesRecordWithParameterlessConstructorAndInitProperties()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, InitOnlyRecordDto>();
            var mapper = new Mapper(config);

            var result = mapper.Map<InitOnlyRecordDto>(new SimpleSource { Id = 1, Name = "Ada" });

            Assert.Equal(1, result.Id);
            Assert.Equal("Ada", result.Name);
        }

        [Fact]
        public void Map_ThrowsDescriptiveException_ForDestinationWithNoParameterlessConstructor()
        {
            // Positional records (and any type without a public parameterless constructor)
            // are not supported as mapping destinations, since instances are created via
            // Activator.CreateInstance. This is a known limitation, not a bug — verify it
            // fails with a clear, actionable message instead of a bare MissingMethodException.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, PositionalRecordDto>();
            var mapper = new Mapper(config);

            var outer = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => mapper.Map<PositionalRecordDto>(new SimpleSource { Id = 1, Name = "Ada" }));

            var inner = Assert.IsType<InvalidOperationException>(outer.InnerException);
            Assert.Contains("PositionalRecordDto", inner.Message);
            Assert.Contains("parameterless constructor", inner.Message);
        }
    }
}
