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

    // Same shape as DepartmentViewModel, but the collection property is declared as an
    // interface rather than the concrete List<T> - exercises the destination-property side of
    // Mapper.TryGetListItemTypes/IsListCompatibleDestination for nested collection properties.
    public class DepartmentIListViewModel
    {
        public string DeptName { get; set; } = string.Empty;
        public IList<EmployeeViewModel> Employees { get; set; } = new List<EmployeeViewModel>();
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

    // Same-name, mismatched-type nested single object (Country/CountryViewModel in the rest of
    // this file always differ in *name* too, so they never exercise the auto-map path — only
    // ForMember). Gadget/Widget share property names across source and destination, so mapping
    // Widget -> WidgetDto with only CreateMap<Gadget, GadgetDto>() and CreateMap<Widget, WidgetDto>()
    // registered (no ForMember) exercises MappingPlanCompiler.BuildNestedObjectAssignment.
    public class Gadget
    {
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class GadgetDto
    {
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class Widget
    {
        public string Label { get; set; } = string.Empty;
        public Gadget Gadget { get; set; } = new();
    }

    public class WidgetDto
    {
        public string Label { get; set; } = string.Empty;
        public GadgetDto Gadget { get; set; } = new();
    }

    // For collection-mapping regression tests: a base/derived pair, used to lock in that
    // List<T> mapping dispatches by the collection's declared/generic item type, not each
    // element's individual runtime type (see MapperTests.Map_MapsHeterogeneousList_...).
    public class Animal
    {
        public string Name { get; set; } = string.Empty;
    }

    public class Dog : Animal
    {
        public string Breed { get; set; } = string.Empty;
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }

    // Self-referential type pair (same-name "Manager" property, both sides typed as the
    // enclosing type itself) - used to verify that resolving nested/list mapping delegates
    // eagerly at plan-compile time was NOT introduced, since doing so would make compiling
    // EmployeeNode's own plan recurse into compiling EmployeeNode's plan again before ever
    // looking at any actual data, StackOverflowing on the very first Map call regardless of
    // how deep (or shallow/acyclic) the real object graph is. The library's documented,
    // disclosed limitation is only for genuinely *circular data* (see CLAUDE.md); a
    // self-referential *type* with acyclic data (e.g. a manager chain that terminates in
    // null) must keep working.
    public class EmployeeNode
    {
        public string Name { get; set; } = string.Empty;
        public EmployeeNode? Manager { get; set; }
    }

    public class EmployeeNodeDto
    {
        public string Name { get; set; } = string.Empty;
        public EmployeeNodeDto? Manager { get; set; }
    }
}
