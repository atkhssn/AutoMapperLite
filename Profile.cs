using AutoMapperLite.Interfaces;

namespace AutoMapperLite
{
    public abstract class Profile
    {
        public abstract void Configure(IMapperConfig config);
    }
}