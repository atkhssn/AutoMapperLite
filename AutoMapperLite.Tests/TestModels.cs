namespace AutoMapperLite.Tests
{
    public class SimpleSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SimpleDestination
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Country
    {
        public string Name { get; set; } = string.Empty;
    }

    public class CountryViewModel
    {
        public string Name { get; set; } = string.Empty;
    }

    public class Organization
    {
        public string OrgName { get; set; } = string.Empty;
        public Country Country { get; set; } = new();
    }

    public class OrganizationViewModel
    {
        public string OrgName { get; set; } = string.Empty;
        public CountryViewModel CountryViewModel { get; set; } = new();
    }

    public class Location
    {
        public string Address { get; set; } = string.Empty;
        public Organization Organization { get; set; } = new();
    }

    public class LocationViewModel
    {
        public string Address { get; set; } = string.Empty;
        public OrganizationViewModel OrganizationViewModel { get; set; } = new();
    }
}
