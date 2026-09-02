using System.Collections;
using System.Linq;
using Xunit;

namespace AutoMapperLite.Tests
{
    public class MapperTests
    {
        [Fact]
        public void Map_TypedOverload_MapsSameAsUntypedOverload()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var result = mapper.Map<SimpleSource, SimpleDestination>(new SimpleSource { Id = 1, Name = "test" });

            Assert.Equal(1, result.Id);
            Assert.Equal("test", result.Name);
        }

        [Fact]
        public void Map_TypedOverload_ReturnsDefault_WhenSourceIsNull()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var result = mapper.Map<SimpleSource?, SimpleDestination>(null);

            Assert.Null(result);
        }

        [Fact]
        public void Map_TypedOverload_Throws_WhenNoMappingRegistered()
        {
            var config = new MapperConfig();
            var mapper = new Mapper(config);

            var ex = Assert.Throws<InvalidOperationException>(
                () => mapper.Map<SimpleSource, SimpleDestination>(new SimpleSource { Id = 1, Name = "test" }));

            Assert.Contains("SimpleSource", ex.Message);
            Assert.Contains("SimpleDestination", ex.Message);
        }

        [Fact]
        public void Map_AutoMapsNestedSingleObjectProperty_WhenSameNameAndTypesRegistered()
        {
            // Widget.Gadget and WidgetDto.Gadget share a property name but differ in type -
            // unlike Organization/OrganizationViewModel elsewhere in this file, this exercises
            // the pure auto-map path (MappingPlanCompiler.BuildNestedObjectAssignment), not
            // ForMember.
            var config = new MapperConfig();
            config.CreateMap<Gadget, GadgetDto>();
            config.CreateMap<Widget, WidgetDto>();
            var mapper = new Mapper(config);

            var result = mapper.Map<WidgetDto>(new Widget
            {
                Label = "box",
                Gadget = new Gadget { SerialNumber = "SN-1" }
            });

            Assert.Equal("box", result.Label);
            Assert.Equal("SN-1", result.Gadget.SerialNumber);
        }

        [Fact]
        public void Map_HandlesSelfReferentialType_WithAcyclicData()
        {
            // EmployeeNode.Manager is typed as EmployeeNode itself - a self-referential *type*
            // graph. The actual data below is acyclic (a 3-link chain terminating in null), which
            // must keep working: nested mapping delegates must stay resolved lazily, at actual
            // Map-call time, not eagerly at plan-compile time - eagerly resolving a nested
            // builder's compiled delegate while still compiling its own plan would recurse into
            // compiling EmployeeNode's plan again before looking at any data, StackOverflowing on
            // the very first Map call regardless of how shallow the real graph is.
            var config = new MapperConfig();
            config.CreateMap<EmployeeNode, EmployeeNodeDto>();
            var mapper = new Mapper(config);

            var ceo = new EmployeeNode { Name = "Ceo", Manager = null };
            var director = new EmployeeNode { Name = "Director", Manager = ceo };
            var engineer = new EmployeeNode { Name = "Engineer", Manager = director };

            var result = mapper.Map<EmployeeNodeDto>(engineer);

            Assert.Equal("Engineer", result.Name);
            Assert.Equal("Director", result.Manager!.Name);
            Assert.Equal("Ceo", result.Manager.Manager!.Name);
            Assert.Null(result.Manager.Manager.Manager);
        }

        [Fact]
        public void Map_MapsListOfObjects_ViaArraySource()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var sources = new[] { new SimpleSource { Id = 1, Name = "a" }, new SimpleSource { Id = 2, Name = "b" } };

            var result = mapper.Map<List<SimpleDestination>>(sources);

            Assert.Equal(2, result.Count);
            Assert.Equal("b", result[1].Name);
        }

        [Fact]
        public void Map_MapsToArrayDestination()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var sources = new List<SimpleSource> { new() { Id = 1, Name = "a" }, new() { Id = 2, Name = "b" } };

            var result = mapper.Map<SimpleDestination[]>(sources);

            Assert.Equal(2, result.Length);
            Assert.Equal("a", result[0].Name);
            Assert.Equal("b", result[1].Name);
        }

        [Fact]
        public void Map_MapsToEmptyArrayDestination_WithoutRequiringRegisteredMap()
        {
            var config = new MapperConfig();
            var mapper = new Mapper(config);

            var result = mapper.Map<SimpleDestination[]>(new List<SimpleSource>());

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(typeof(IList<SimpleDestination>))]
        [InlineData(typeof(ICollection<SimpleDestination>))]
        [InlineData(typeof(IReadOnlyList<SimpleDestination>))]
        [InlineData(typeof(IReadOnlyCollection<SimpleDestination>))]
        [InlineData(typeof(IEnumerable<SimpleDestination>))]
        public void Map_MapsToListCompatibleInterfaceDestination(Type destinationType)
        {
            // IMapper.Map<TDestination> is generic on TDestination, which xunit's [Theory] can't
            // parameterize directly - invoke it via MakeGenericMethod instead to still verify
            // every one of these interface shapes end-to-end through the real public API.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);
            var sources = new List<SimpleSource> { new() { Id = 1, Name = "a" }, new() { Id = 2, Name = "b" } };

            var mapMethod = typeof(Mapper).GetMethod(nameof(Mapper.Map), new[] { typeof(object) })!
                .MakeGenericMethod(destinationType);
            var result = (System.Collections.IEnumerable)mapMethod.Invoke(mapper, new object?[] { sources })!;

            var items = result.Cast<SimpleDestination>().ToList();
            Assert.Equal(2, items.Count);
            Assert.Equal("b", items[1].Name);
        }

        [Fact]
        public void Map_AutoMapsNestedListProperty_WhenDestinationPropertyIsListCompatibleInterface()
        {
            var config = new MapperConfig();
            config.CreateMap<Employee, EmployeeViewModel>();
            config.CreateMap<Department, DepartmentIListViewModel>();
            var mapper = new Mapper(config);

            var result = mapper.Map<DepartmentIListViewModel>(new Department
            {
                DeptName = "Engineering",
                Employees = new List<Employee> { new() { Name = "Ada" }, new() { Name = "Alan" } }
            });

            Assert.Equal(2, result.Employees.Count);
            Assert.Equal("Ada", result.Employees[0].Name);
            Assert.Equal("Alan", result.Employees[1].Name);
        }

        [Fact]
        public void Map_MapsListOfObjects_ViaCustomIEnumerableSource()
        {
            // A LINQ iterator implements IEnumerable<T> but is neither List<T> nor an array -
            // exercises the IEnumerable<T> interface-inspection fallback in
            // Mapper.TryGetDeclaredItemType, not the List<T>/array fast checks.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            IEnumerable<SimpleSource> sources = new List<SimpleSource>
            {
                new() { Id = 1, Name = "a" },
                new() { Id = 2, Name = "b" },
            }.Select(s => s);

            var result = mapper.Map<List<SimpleDestination>>(sources);

            Assert.Equal(2, result.Count);
            Assert.Equal("b", result[1].Name);
        }

        [Fact]
        public void Map_MapsListOfObjects_ViaNonGenericEnumerableSource()
        {
            // A non-generic ArrayList can't expose a declared item type at all - exercises the
            // Mapper.MapListByRuntimeType fallback (per-item runtime-type dispatch), not the
            // strongly-typed resolve-once loop.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var sources = new ArrayList
            {
                new SimpleSource { Id = 1, Name = "a" },
                new SimpleSource { Id = 2, Name = "b" },
            };

            var result = mapper.Map<List<SimpleDestination>>(sources);

            Assert.Equal(2, result.Count);
            Assert.Equal("b", result[1].Name);
        }

        [Fact]
        public void Map_MapsHeterogeneousList_UsingDeclaredItemTypesMap_NotEachItemsRuntimeType()
        {
            // Documented behavior (see CLAUDE.md): List<T> destination mapping resolves the item
            // map from the source collection's *declared* item type once, not from each
            // element's individual runtime type. A List<Animal> containing a Dog therefore maps
            // every element (including the Dog) through Animal's registered map - Dog's own
            // extra property (Breed) is simply not part of that map, same as if the list had
            // been declared/consumed as List<Animal> anywhere else in C#. No Dog-specific map is
            // required, and none is registered here.
            var config = new MapperConfig();
            config.CreateMap<Animal, AnimalDto>();
            var mapper = new Mapper(config);

            var sources = new List<Animal>
            {
                new Animal { Name = "Cat" },
                new Dog { Name = "Rex", Breed = "Labrador" },
            };

            var result = mapper.Map<List<AnimalDto>>(sources);

            Assert.Equal(2, result.Count);
            Assert.Equal("Cat", result[0].Name);
            Assert.Equal("Rex", result[1].Name);
        }

        [Fact]
        public void Map_MapsEmptyList_WithoutRequiringRegisteredMap()
        {
            var config = new MapperConfig();
            var mapper = new Mapper(config);

            var result = mapper.Map<List<SimpleDestination>>(new List<SimpleSource>());

            Assert.Empty(result);
        }

        [Fact]
        public void Map_PreservesConfigIsolation_ForTypedOverload_AcrossDifferentConfigs()
        {
            var configA = new MapperConfig();
            configA.CreateMap<SimpleSource, SimpleDestination>()
                .ForMember(d => d.Name, s => s.Name + "-A");
            var mapperA = new Mapper(configA);

            var configB = new MapperConfig();
            configB.CreateMap<SimpleSource, SimpleDestination>()
                .ForMember(d => d.Name, s => s.Name + "-B");
            var mapperB = new Mapper(configB);

            var source = new SimpleSource { Id = 1, Name = "x" };

            Assert.Equal("x-A", mapperA.Map<SimpleSource, SimpleDestination>(source).Name);
            Assert.Equal("x-B", mapperB.Map<SimpleSource, SimpleDestination>(source).Name);
            // And the untyped overload, which shares the same per-instance entry-point cache,
            // must agree with the typed one on each mapper.
            Assert.Equal("x-A", mapperA.Map<SimpleDestination>(source).Name);
            Assert.Equal("x-B", mapperB.Map<SimpleDestination>(source).Name);
        }

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
            // The mapping engine dispatches through compiled delegates, not reflection
            // Invoke(), so this now throws InvalidOperationException directly (previously
            // it arrived wrapped in a TargetInvocationException — an implementation-detail
            // leak from the old MethodInfo.Invoke-based dispatch, not a documented contract).
            var config = new MapperConfig();
            var mapper = new Mapper(config);

            var ex = Assert.Throws<InvalidOperationException>(
                () => mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" }));

            Assert.Contains("SimpleSource", ex.Message);
            Assert.Contains("SimpleDestination", ex.Message);
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
            // Regression test, twice over:
            // 1) Originally: the interpreted mapper held `destination` as a TDestination
            //    local; for a struct, each PropertyInfo.SetValue call boxed a fresh
            //    temporary copy and mutated that instead of the real local, silently
            //    discarding every assignment.
            // 2) During the move to a compiled Expression-tree plan: destination
            //    construction switched to Expression.New(Type), which (correctly)
            //    handles structs without an explicit parameterless constructor — an
            //    earlier attempt using type.GetConstructor(Type.EmptyTypes) does NOT
            //    find one for such structs (a real reflection gotcha) and this test
            //    caught that regression immediately.
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

            // Compiled dispatch (see the "no mapping registered" test above), so this
            // throws InvalidOperationException directly, not wrapped in a
            // TargetInvocationException.
            var ex = Assert.Throws<InvalidOperationException>(
                () => mapper.Map<SimpleDestination>(new SimpleSource { Id = 1, Name = "test" }));

            Assert.Contains("Id", ex.Message);
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
        public void Map_ArrayAndInterfaceDestinations_AreThreadSafe_WhenManyThreadsRaceTheFirstCollectionMap()
        {
            // Array/interface collection destinations (4.4.0: Mapper.IsListCompatibleDestination,
            // the IsArray branch in Map<TDestination>(object)) all route through the same
            // GetOrBuildListMapper/_listMappers cache that Map_ListMapperCacheIsThreadSafe above
            // already races - this test exists to also race the *new* code paths built on top of
            // it (the array-copy step, and casting the shared List<T> result to each interface
            // type) on a brand-new mapper's very first call, since those paths didn't exist when
            // that test was written.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            Parallel.For(0, 500, i =>
            {
                try
                {
                    var sources = new List<SimpleSource> { new() { Id = i, Name = "a" }, new() { Id = i + 1, Name = "b" } };

                    var array = mapper.Map<SimpleDestination[]>(sources);
                    if (array.Length != 2 || array[1].Name != "b") throw new Exception("Array map mismatch");

                    var asIList = mapper.Map<IList<SimpleDestination>>(sources);
                    if (asIList.Count != 2 || asIList[1].Name != "b") throw new Exception("IList map mismatch");

                    var asEnumerable = mapper.Map<IEnumerable<SimpleDestination>>(sources).ToList();
                    if (asEnumerable.Count != 2 || asEnumerable[1].Name != "b") throw new Exception("IEnumerable map mismatch");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        [Fact]
        public void Map_ListMapperCacheIsThreadSafe_WhenManyThreadsRaceTheFirstCollectionMap()
        {
            // The strongly-typed list-mapper cache (Mapper._listMappers) and its lazily-resolved
            // captured `builder` field (see Mapper.BuildListMapper) are new, race-sensitive state
            // introduced by the collection-mapping redesign - race many threads against a
            // brand-new config/mapper's very first List<T> map call, mixed with the typed
            // single-object overload on the same mapper instance to also exercise _entryPoints
            // under the same race.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            Parallel.For(0, 500, i =>
            {
                try
                {
                    var list = mapper.Map<List<SimpleDestination>>(new List<SimpleSource>
                    {
                        new() { Id = i, Name = "a" },
                        new() { Id = i + 1, Name = "b" },
                    });
                    if (list.Count != 2 || list[1].Name != "b") throw new Exception("List map mismatch");

                    var single = mapper.Map<SimpleSource, SimpleDestination>(new SimpleSource { Id = i, Name = $"n{i}" });
                    if (single.Id != i) throw new Exception("Typed map mismatch");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        [Fact]
        public void Map_CompiledPlanIsThreadSafe_WhenManyThreadsRaceTheFirstMapCall()
        {
            // The compiled mapping plan (see MappingPlanCompiler) is built lazily via
            // LazyInitializer.EnsureInitialized on MapBuilder's first Map call — this is a
            // genuinely new concurrency-sensitive path introduced by compiling the plan instead
            // of interpreting it. Deliberately gives every thread the exact same brand-new
            // config with zero warmup, so they all race the very first compile simultaneously.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            var mapper = new Mapper(config);

            var results = new System.Collections.Concurrent.ConcurrentBag<SimpleDestination>();
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            Parallel.For(0, 500, i =>
            {
                try
                {
                    results.Add(mapper.Map<SimpleDestination>(new SimpleSource { Id = i, Name = $"n{i}" }));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
            Assert.Equal(500, results.Count);
            Assert.All(results, r => Assert.StartsWith("n", r.Name));
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
            // are not supported as mapping destinations, since instances are created via a
            // compiled constructor call. This is a known limitation, not a bug — verify it
            // fails with a clear, actionable message instead of a bare reflection exception.
            // Thrown directly (compiled dispatch, no reflection Invoke wrapper) — see the
            // "no mapping registered" test above for why.
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, PositionalRecordDto>();
            var mapper = new Mapper(config);

            var ex = Assert.Throws<InvalidOperationException>(
                () => mapper.Map<PositionalRecordDto>(new SimpleSource { Id = 1, Name = "Ada" }));

            Assert.Contains("PositionalRecordDto", ex.Message);
            Assert.Contains("parameterless constructor", ex.Message);
        }
    }
}
