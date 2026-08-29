namespace AutoMapperLite.Benchmarks
{
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
}
