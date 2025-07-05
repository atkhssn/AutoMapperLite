namespace AutoMapperLite.Mapping;

public class MapRule
{
    public string DestinationProperty { get; set; } = string.Empty;
    public Func<object, object?>? ValueResolver { get; set; }
}