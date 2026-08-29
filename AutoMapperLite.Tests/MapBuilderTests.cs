using Xunit;

namespace AutoMapperLite.Tests
{
    public class MapBuilderTests
    {
        [Fact]
        public void ForMember_RegistersMappingUnderTopLevelPropertyName()
        {
            var builder = new MapBuilder<Organization, OrganizationViewModel>();

            builder.ForMember(dest => dest.OrgName, src => src.OrgName);

            Assert.True(builder.MemberMappings.ContainsKey("OrgName"));
        }

        [Fact]
        public void ForPath_RegistersMappingUnderDotDelimitedPath()
        {
            var builder = new MapBuilder<Location, LocationViewModel>();

            builder.ForPath(
                dest => dest.OrganizationViewModel.CountryViewModel.Name,
                src => src.Organization.Country.Name);

            Assert.True(builder.MemberMappings.ContainsKey(
                "OrganizationViewModel.CountryViewModel.Name"));
        }

        [Fact]
        public void ForMember_MapFunc_IsInvokedWithSource()
        {
            var builder = new MapBuilder<SimpleSource, SimpleDestination>();
            builder.ForMember(dest => dest.Name, src => src.Name + "-suffix");

            var mapFunc = builder.MemberMappings["Name"];
            var result = mapFunc(new SimpleSource { Name = "value" });

            Assert.Equal("value-suffix", result);
        }
    }
}
