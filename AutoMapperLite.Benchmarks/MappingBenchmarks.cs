using AutoMapperLite.Interfaces;
using BenchmarkDotNet.Attributes;

namespace AutoMapperLite.Benchmarks
{
    [MemoryDiagnoser]
    public class MappingBenchmarks
    {
        private IMapper _mapper = null!;
        private SimpleSource _simpleSource = null!;
        private Organization _organization = null!;
        private List<SimpleSource> _sourceList = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new MapperConfig();
            config.CreateMap<SimpleSource, SimpleDestination>();
            config.CreateMap<Country, CountryViewModel>();
            config.CreateMap<Organization, OrganizationViewModel>()
                .ForMember(dest => dest.CountryViewModel,
                           src => new Mapper(config).Map<CountryViewModel>(src.Country));

            _mapper = new Mapper(config);

            _simpleSource = new SimpleSource { Id = 1, Name = "Benchmark" };
            _organization = new Organization
            {
                OrgName = "Acme",
                Country = new Country { Name = "Wonderland" }
            };
            _sourceList = Enumerable.Range(0, 100)
                .Select(i => new SimpleSource { Id = i, Name = $"Item {i}" })
                .ToList();
        }

        [Benchmark(Baseline = true)]
        public SimpleDestination MapSimpleObject() => _mapper.Map<SimpleDestination>(_simpleSource);

        [Benchmark]
        public OrganizationViewModel MapNestedObject() => _mapper.Map<OrganizationViewModel>(_organization);

        [Benchmark]
        public List<SimpleDestination> MapCollection() => _mapper.Map<List<SimpleDestination>>(_sourceList);
    }
}
