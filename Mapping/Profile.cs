using AutoMapperLite.Interfaces;

namespace AutoMapperLite.Mapping
{
    public abstract class Profile : IProfile
    {
        public abstract void Configure(MapperConfig config);

        protected MapBuilder<TSource, TDestination> CreateMap<TSource, TDestination>(MapperConfig config)
        {
            return config.CreateMap<TSource, TDestination>();
        }
    }
}