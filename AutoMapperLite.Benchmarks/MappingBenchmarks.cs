using AutoMapper;
using BenchmarkDotNet.Attributes;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using AutoMapperLiteIMapper = AutoMapperLite.Interfaces.IMapper;
using AutoMapperIMapper = AutoMapper.IMapper;

namespace AutoMapperLite.Benchmarks
{
    /// <summary>Shared setup for all "warm" comparison benchmarks: AutoMapperLite, AutoMapper, Mapster, and hand-written manual mapping.</summary>
    public abstract class ComparisonBenchmarkBase
    {
        internal AutoMapperLiteIMapper AutoMapperLite = null!;
        internal AutoMapperIMapper AutoMapper = null!;
        internal TypeAdapterConfig MapsterConfig = null!;

        internal SimpleSource SimpleSource = null!;
        internal MediumSource MediumSource = null!;
        internal Organization OrganizationSource = null!;
        internal Level1 DeepNestedSource = null!;
        internal PersonSource PersonSource = null!;
        internal List<SimpleSource> SourceList100 = null!;
        internal List<SimpleSource> SourceList5000 = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new MapperConfig();
            // One shared Mapper instance, captured by every ForMember closure below - matches
            // CLAUDE.md's own documented best practice ("capturing a Mapper built from the same
            // config in the closure"). Building a new Mapper(config) per lambda invocation (the
            // previous version of this benchmark did exactly that) allocates 1-3 extra Mapper
            // instances per DeepNested call and defeats any per-instance caching - it was an
            // accidental self-inflicted handicap in the benchmark setup, not a real cost of the
            // mapping engine.
            var mapper = new Mapper(config);
            config.CreateMap<SimpleSource, SimpleDestination>();
            config.CreateMap<MediumSource, MediumDestination>();
            config.CreateMap<Country, CountryViewModel>();
            config.CreateMap<Organization, OrganizationViewModel>()
                .ForMember(dest => dest.CountryViewModel,
                           src => mapper.Map<CountryViewModel>(src.Country));
            // Deliberately NOT using ForMember for these Child properties: the source and
            // destination property are both named "Child" and each level's type pair is
            // separately registered above/below, so AutoMapperLite's own same-name auto-map
            // path (MappingPlanCompiler.BuildNestedObjectAssignment) already resolves the
            // nested MapBuilder once at compile time and bakes it in as a constant - the fast
            // 4.2.0 dispatch path. Routing this through ForMember + mapper.Map<T>(object)
            // instead (as an earlier version of this benchmark did) forces every nested call
            // through the slower untyped entry-point cache/GetType()/boxing path for no
            // reason, which isn't representative of how this scenario should be configured -
            // neither the AutoMapper nor Mapster config below uses an equivalent explicit
            // per-level mapping for this same chain; both rely on their own convention engine,
            // so this keeps the comparison apples-to-apples.
            config.CreateMap<Level4, Level4Dto>();
            config.CreateMap<Level3, Level3Dto>();
            config.CreateMap<Level2, Level2Dto>();
            config.CreateMap<Level1, Level1Dto>();
            var currentYear = DateTime.UtcNow.Year;
            config.CreateMap<PersonSource, PersonDestination>()
                .ForMember(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}")
                .ForMember(dest => dest.Age, src => currentYear - src.BirthYear);
            AutoMapperLite = mapper;

            var amConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<SimpleSource, SimpleDestination>();
                cfg.CreateMap<MediumSource, MediumDestination>();
                cfg.CreateMap<Country, CountryViewModel>();
                cfg.CreateMap<Organization, OrganizationViewModel>()
                    .ForMember(d => d.CountryViewModel, o => o.MapFrom(s => s.Country));
                cfg.CreateMap<Level4, Level4Dto>();
                cfg.CreateMap<Level3, Level3Dto>();
                cfg.CreateMap<Level2, Level2Dto>();
                cfg.CreateMap<Level1, Level1Dto>();
                cfg.CreateMap<PersonSource, PersonDestination>()
                    .ForMember(d => d.FullName, o => o.MapFrom(s => $"{s.FirstName} {s.LastName}"))
                    .ForMember(d => d.Age, o => o.MapFrom(s => currentYear - s.BirthYear));
            }, NullLoggerFactory.Instance);
            AutoMapper = amConfig.CreateMapper();

            MapsterConfig = new TypeAdapterConfig();
            MapsterConfig.NewConfig<SimpleSource, SimpleDestination>();
            MapsterConfig.NewConfig<MediumSource, MediumDestination>();
            MapsterConfig.NewConfig<Country, CountryViewModel>();
            MapsterConfig.NewConfig<Organization, OrganizationViewModel>()
                .Map(d => d.CountryViewModel, s => s.Country);
            MapsterConfig.NewConfig<Level4, Level4Dto>();
            MapsterConfig.NewConfig<Level3, Level3Dto>();
            MapsterConfig.NewConfig<Level2, Level2Dto>();
            MapsterConfig.NewConfig<Level1, Level1Dto>();
            MapsterConfig.NewConfig<PersonSource, PersonDestination>()
                .Map(d => d.FullName, s => s.FirstName + " " + s.LastName)
                .Map(d => d.Age, s => currentYear - s.BirthYear);

            SimpleSource = new SimpleSource { Id = 1, Name = "Benchmark" };
            MediumSource = new MediumSource
            {
                Id = 1,
                Name = "Benchmark",
                Email = "benchmark@example.com",
                IsActive = true,
                Score = 98.6,
                Balance = 1234.56m,
                CreatedAt = new DateTime(2024, 1, 1),
                ExternalId = 123456789L,
            };
            OrganizationSource = new Organization
            {
                OrgName = "Acme",
                Country = new Country { Name = "Wonderland" }
            };
            DeepNestedSource = new Level1
            {
                Name = "root",
                Child = new Level2 { Child = new Level3 { Child = new Level4 { Value = "leaf" } } }
            };
            PersonSource = new PersonSource { FirstName = "Ada", LastName = "Lovelace", BirthYear = 1990 };
            SourceList100 = Enumerable.Range(0, 100)
                .Select(i => new SimpleSource { Id = i, Name = $"Item {i}" })
                .ToList();
            SourceList5000 = Enumerable.Range(0, 5000)
                .Select(i => new SimpleSource { Id = i, Name = $"Item {i}" })
                .ToList();
        }
    }

    [MemoryDiagnoser]
    public class SimpleMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public SimpleDestination Manual() => new() { Id = SimpleSource.Id, Name = SimpleSource.Name };

        // Object API: dispatches by SimpleSource.GetType() at runtime through a per-Mapper-instance
        // cache. This is what a consumer gets from IMapper.Map<TDestination>(object) - the shape
        // most existing code uses, e.g. when the source's static type isn't known at the call site.
        [Benchmark]
        public SimpleDestination AutoMapperLite_() => AutoMapperLite.Map<SimpleDestination>(SimpleSource);

        // Generic API: both types known at the call site, so this skips GetType() and the
        // entry-point cache entirely - see IMapper.Map<TSource,TDestination> and CLAUDE.md's "Why
        // the dispatch caches..." section. Prefer this shape in any hot loop where the source type
        // is statically known.
        [Benchmark]
        public SimpleDestination AutoMapperLite_Typed() => AutoMapperLite.Map<SimpleSource, SimpleDestination>(SimpleSource);

        [Benchmark]
        public SimpleDestination AutoMapper_() => AutoMapper.Map<SimpleDestination>(SimpleSource);

        [Benchmark]
        public SimpleDestination Mapster_() => SimpleSource.Adapt<SimpleDestination>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class MediumMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public MediumDestination Manual() => new()
        {
            Id = MediumSource.Id,
            Name = MediumSource.Name,
            Email = MediumSource.Email,
            IsActive = MediumSource.IsActive,
            Score = MediumSource.Score,
            Balance = MediumSource.Balance,
            CreatedAt = MediumSource.CreatedAt,
            ExternalId = MediumSource.ExternalId,
        };

        [Benchmark]
        public MediumDestination AutoMapperLite_() => AutoMapperLite.Map<MediumDestination>(MediumSource);

        [Benchmark]
        public MediumDestination AutoMapperLite_Typed() => AutoMapperLite.Map<MediumSource, MediumDestination>(MediumSource);

        [Benchmark]
        public MediumDestination AutoMapper_() => AutoMapper.Map<MediumDestination>(MediumSource);

        [Benchmark]
        public MediumDestination Mapster_() => MediumSource.Adapt<MediumDestination>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class NestedMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public OrganizationViewModel Manual() => new()
        {
            OrgName = OrganizationSource.OrgName,
            CountryViewModel = new CountryViewModel { Name = OrganizationSource.Country.Name },
        };

        [Benchmark]
        public OrganizationViewModel AutoMapperLite_() => AutoMapperLite.Map<OrganizationViewModel>(OrganizationSource);

        [Benchmark]
        public OrganizationViewModel AutoMapperLite_Typed() => AutoMapperLite.Map<Organization, OrganizationViewModel>(OrganizationSource);

        [Benchmark]
        public OrganizationViewModel AutoMapper_() => AutoMapper.Map<OrganizationViewModel>(OrganizationSource);

        [Benchmark]
        public OrganizationViewModel Mapster_() => OrganizationSource.Adapt<OrganizationViewModel>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class DeepNestedMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public Level1Dto Manual() => new()
        {
            Name = DeepNestedSource.Name,
            Child = new Level2Dto
            {
                Child = new Level3Dto
                {
                    Child = new Level4Dto { Value = DeepNestedSource.Child.Child.Child.Value }
                }
            }
        };

        [Benchmark]
        public Level1Dto AutoMapperLite_() => AutoMapperLite.Map<Level1Dto>(DeepNestedSource);

        [Benchmark]
        public Level1Dto AutoMapperLite_Typed() => AutoMapperLite.Map<Level1, Level1Dto>(DeepNestedSource);

        [Benchmark]
        public Level1Dto AutoMapper_() => AutoMapper.Map<Level1Dto>(DeepNestedSource);

        [Benchmark]
        public Level1Dto Mapster_() => DeepNestedSource.Adapt<Level1Dto>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class CustomMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public PersonDestination Manual() => new()
        {
            FullName = PersonSource.FirstName + " " + PersonSource.LastName,
            Age = DateTime.UtcNow.Year - PersonSource.BirthYear,
        };

        [Benchmark]
        public PersonDestination AutoMapperLite_() => AutoMapperLite.Map<PersonDestination>(PersonSource);

        [Benchmark]
        public PersonDestination AutoMapperLite_Typed() => AutoMapperLite.Map<PersonSource, PersonDestination>(PersonSource);

        [Benchmark]
        public PersonDestination AutoMapper_() => AutoMapper.Map<PersonDestination>(PersonSource);

        [Benchmark]
        public PersonDestination Mapster_() => PersonSource.Adapt<PersonDestination>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class Collection100MappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public List<SimpleDestination> Manual()
        {
            var result = new List<SimpleDestination>(SourceList100.Count);
            foreach (var item in SourceList100) result.Add(new SimpleDestination { Id = item.Id, Name = item.Name });
            return result;
        }

        [Benchmark]
        public List<SimpleDestination> AutoMapperLite_() => AutoMapperLite.Map<List<SimpleDestination>>(SourceList100);

        [Benchmark]
        public List<SimpleDestination> AutoMapper_() => AutoMapper.Map<List<SimpleDestination>>(SourceList100);

        [Benchmark]
        public List<SimpleDestination> Mapster_() => SourceList100.Adapt<List<SimpleDestination>>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class Collection5000MappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public List<SimpleDestination> Manual()
        {
            var result = new List<SimpleDestination>(SourceList5000.Count);
            foreach (var item in SourceList5000) result.Add(new SimpleDestination { Id = item.Id, Name = item.Name });
            return result;
        }

        [Benchmark]
        public List<SimpleDestination> AutoMapperLite_() => AutoMapperLite.Map<List<SimpleDestination>>(SourceList5000);

        [Benchmark]
        public List<SimpleDestination> AutoMapper_() => AutoMapper.Map<List<SimpleDestination>>(SourceList5000);

        [Benchmark]
        public List<SimpleDestination> Mapster_() => SourceList5000.Adapt<List<SimpleDestination>>(MapsterConfig);
    }

    /// <summary>
    /// Collection scaling: how mean time per call grows with source-collection size. Complements
    /// Collection100MappingBenchmarks/Collection5000MappingBenchmarks with sizes further in both
    /// directions (10 items, and 10,000/100,000 items) to check the per-item cost stays linear
    /// rather than degrading, since all four libraries here resolve their element mapper once and
    /// then loop - a size-dependent super-linear blowup would indicate a per-item resolution cost
    /// slipping back into the hot loop.
    /// </summary>
    [MemoryDiagnoser]
    public class CollectionScalingBenchmarks
    {
        [Params(10, 1_000, 10_000, 100_000)]
        public int Size;

        private AutoMapperLiteIMapper _autoMapperLite = null!;
        private AutoMapperIMapper _autoMapper = null!;
        private TypeAdapterConfig _mapsterConfig = null!;
        private List<SimpleSource> _sourceList = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            _autoMapperLite = new Mapper(config);

            var amConfig = new MapperConfiguration(
                cfg => cfg.CreateMap<SimpleSource, SimpleDestination>(), NullLoggerFactory.Instance);
            _autoMapper = amConfig.CreateMapper();

            _mapsterConfig = new TypeAdapterConfig();
            _mapsterConfig.NewConfig<SimpleSource, SimpleDestination>();

            _sourceList = Enumerable.Range(0, Size)
                .Select(i => new SimpleSource { Id = i, Name = $"Item {i}" })
                .ToList();
        }

        [Benchmark(Baseline = true)]
        public List<SimpleDestination> Manual()
        {
            var result = new List<SimpleDestination>(_sourceList.Count);
            foreach (var item in _sourceList) result.Add(new SimpleDestination { Id = item.Id, Name = item.Name });
            return result;
        }

        [Benchmark]
        public List<SimpleDestination> AutoMapperLite_() => _autoMapperLite.Map<List<SimpleDestination>>(_sourceList);

        [Benchmark]
        public List<SimpleDestination> AutoMapper_() => _autoMapper.Map<List<SimpleDestination>>(_sourceList);

        [Benchmark]
        public List<SimpleDestination> Mapster_() => _sourceList.Adapt<List<SimpleDestination>>(_mapsterConfig);
    }

    /// <summary>
    /// Cold-start: the cost of the *first* mapping call for a type pair, including whatever
    /// configuration/compilation each library does lazily on first use (AutoMapperLite compiles
    /// its Expression-tree plan; AutoMapper/Mapster build/validate their own configuration).
    /// A fresh config + mapper instance is built in IterationSetup so [Benchmark] measures only
    /// the first Map call, not steady-state warm throughput (see WarmMapping benchmarks above
    /// for that).
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class ColdStartBenchmarks
    {
        private SimpleSource _source = null!;
        private AutoMapperLiteIMapper _autoMapperLite = null!;
        private AutoMapperIMapper _autoMapper = null!;
        private TypeAdapterConfig _mapsterConfig = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _source = new SimpleSource { Id = 1, Name = "Benchmark" };
        }

        [IterationSetup(Target = nameof(AutoMapperLite_ColdStart))]
        public void SetupAutoMapperLite()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            _autoMapperLite = new Mapper(config);
        }

        [IterationSetup(Target = nameof(AutoMapper_ColdStart))]
        public void SetupAutoMapper()
        {
            var amConfig = new MapperConfiguration(cfg => cfg.CreateMap<SimpleSource, SimpleDestination>(), NullLoggerFactory.Instance);
            _autoMapper = amConfig.CreateMapper();
        }

        [IterationSetup(Target = nameof(Mapster_ColdStart))]
        public void SetupMapster()
        {
            _mapsterConfig = new TypeAdapterConfig();
            _mapsterConfig.NewConfig<SimpleSource, SimpleDestination>();
        }

        [Benchmark(Baseline = true)]
        public SimpleDestination AutoMapperLite_ColdStart() => _autoMapperLite.Map<SimpleDestination>(_source);

        [Benchmark]
        public SimpleDestination AutoMapper_ColdStart() => _autoMapper.Map<SimpleDestination>(_source);

        [Benchmark]
        public SimpleDestination Mapster_ColdStart() => _source.Adapt<SimpleDestination>(_mapsterConfig);
    }
}
