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

    public class Employee
    {
        public string Name { get; set; } = string.Empty;
    }

    public class EmployeeViewModel
    {
        public string Name { get; set; } = string.Empty;
    }

    public class Department
    {
        public string DeptName { get; set; } = string.Empty;
        public List<Employee> Employees { get; set; } = new();
    }

    public class DepartmentViewModel
    {
        public string DeptName { get; set; } = string.Empty;
        public List<EmployeeViewModel> Employees { get; set; } = new();
    }

    public struct PointStruct
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public struct PointStructDto
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public enum OrderStatus { Pending, Shipped, Delivered }

    public class Order
    {
        public OrderStatus Status { get; set; }
    }

    public class OrderDto
    {
        public OrderStatus Status { get; set; }
    }

    public class NullSource
    {
        public string? Text { get; set; }
    }

    public class DefaultedDestination
    {
        public string Text { get; set; } = "server-default";
    }

    public class InitOnlyRecordDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public record PositionalRecordDto(int Id, string Name);
}
