namespace AutoMapperLite.Benchmarks
{
    // ---------- Simple: 2 properties ----------
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

    // ---------- Medium: 8 properties, mixed primitive types ----------
    public class MediumSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public double Score { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public long ExternalId { get; set; }
    }

    public class MediumDestination
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public double Score { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public long ExternalId { get; set; }
    }

    // ---------- Nested: one level ----------
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

    // ---------- Deep nested: four levels ----------
    public class Level4 { public string Value { get; set; } = string.Empty; }
    public class Level3 { public Level4 Child { get; set; } = new(); }
    public class Level2 { public Level3 Child { get; set; } = new(); }
    public class Level1 { public Level2 Child { get; set; } = new(); public string Name { get; set; } = string.Empty; }

    public class Level4Dto { public string Value { get; set; } = string.Empty; }
    public class Level3Dto { public Level4Dto Child { get; set; } = new(); }
    public class Level2Dto { public Level3Dto Child { get; set; } = new(); }
    public class Level1Dto { public Level2Dto Child { get; set; } = new(); public string Name { get; set; } = string.Empty; }

    // ---------- Custom mapping: requires a transform, not a plain copy ----------
    public class PersonSource
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int BirthYear { get; set; }
    }

    public class PersonDestination
    {
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
