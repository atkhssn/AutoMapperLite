using AutoMapper;
using BenchmarkDotNet.Attributes;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using AutoMapperLiteIMapper = AutoMapperLite.Interfaces.IMapper;
using AutoMapperIMapper = AutoMapper.IMapper;

namespace AutoMapperLite.Benchmarks
{
    /// <summary>Shared setup for all comparison benchmarks: AutoMapperLite, AutoMapper, Mapster, and hand-written manual mapping.</summary>
    public abstract class ComparisonBenchmarkBase
    {
        protected AutoMapperLiteIMapper AutoMapperLite = null!;
        protected AutoMapperIMapper AutoMapper = null!;
        protected TypeAdapterConfig MapsterConfig = null!;

        protected SimpleSource SimpleSource = null!;
        protected Organization OrganizationSource = null!;
        protected List<SimpleSource> SourceList = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            config.CreateMap<Country, CountryViewModel>();
            config.CreateMap<Organization, OrganizationViewModel>()
                .ForMember(dest => dest.CountryViewModel,
                           src => new Mapper(config).Map<CountryViewModel>(src.Country));
            AutoMapperLite = new Mapper(config);

            var amConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<SimpleSource, SimpleDestination>();
                cfg.CreateMap<Country, CountryViewModel>();
                cfg.CreateMap<Organization, OrganizationViewModel>();
            }, NullLoggerFactory.Instance);
            AutoMapper = amConfig.CreateMapper();

            MapsterConfig = new TypeAdapterConfig();
            MapsterConfig.NewConfig<SimpleSource, SimpleDestination>();
            MapsterConfig.NewConfig<Country, CountryViewModel>();
            MapsterConfig.NewConfig<Organization, OrganizationViewModel>();

            SimpleSource = new SimpleSource { Id = 1, Name = "Benchmark" };
            OrganizationSource = new Organization
            {
                OrgName = "Acme",
                Country = new Country { Name = "Wonderland" }
            };
            SourceList = Enumerable.Range(0, 100)
                .Select(i => new SimpleSource { Id = i, Name = $"Item {i}" })
                .ToList();
        }
    }

    [MemoryDiagnoser]
    public class SimpleMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public SimpleDestination Manual() => new()
        {
            Id = SimpleSource.Id,
            Name = SimpleSource.Name,
        };

        [Benchmark]
        public SimpleDestination AutoMapperLite_() => AutoMapperLite.Map<SimpleDestination>(SimpleSource);

        [Benchmark]
        public SimpleDestination AutoMapper_() => AutoMapper.Map<SimpleDestination>(SimpleSource);

        [Benchmark]
        public SimpleDestination Mapster_() => SimpleSource.Adapt<SimpleDestination>(MapsterConfig);
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
        public OrganizationViewModel AutoMapper_() => AutoMapper.Map<OrganizationViewModel>(OrganizationSource);

        [Benchmark]
        public OrganizationViewModel Mapster_() => OrganizationSource.Adapt<OrganizationViewModel>(MapsterConfig);
    }

    [MemoryDiagnoser]
    public class CollectionMappingBenchmarks : ComparisonBenchmarkBase
    {
        [Benchmark(Baseline = true)]
        public List<SimpleDestination> Manual()
        {
            var result = new List<SimpleDestination>(SourceList.Count);
            foreach (var item in SourceList)
            {
                result.Add(new SimpleDestination { Id = item.Id, Name = item.Name });
            }
            return result;
        }

        [Benchmark]
        public List<SimpleDestination> AutoMapperLite_() => AutoMapperLite.Map<List<SimpleDestination>>(SourceList);

        [Benchmark]
        public List<SimpleDestination> AutoMapper_() => AutoMapper.Map<List<SimpleDestination>>(SourceList);

        [Benchmark]
        public List<SimpleDestination> Mapster_() => SourceList.Adapt<List<SimpleDestination>>(MapsterConfig);
    }
}
