using System.Linq.Expressions;
using System.Reflection;

namespace AutoMapperLite.Mapping;

public class MapConfig<TSource, TDestination>
{
    private readonly List<MapRule> _rules = new();

    public MapConfig<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destSelector,
        Func<TSource, object?> resolver)
    {
        if (destSelector.Body is MemberExpression member)
        {
            _rules.Add(new MapRule
            {
                DestinationProperty = member.Member.Name,
                ValueResolver = src => resolver((TSource)src)
            });
        }
        return this;
    }

    internal TDestination Map(TSource source, MapperConfig parentConfig)
    {
        var dest = Activator.CreateInstance<TDestination>()!;
        var destProps = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var destProp in destProps)
        {
            var rule = _rules.FirstOrDefault(r => r.DestinationProperty == destProp.Name);
            if (rule?.ValueResolver != null)
            {
                destProp.SetValue(dest, rule.ValueResolver(source ?? default!));
                continue;
            }

            var sourceProp = sourceProps.FirstOrDefault(p => p.Name == destProp.Name);
            if (sourceProp != null)
            {
                var sourceValue = sourceProp.GetValue(source);
                if (sourceValue != null &&
                    parentConfig.TryMap(sourceProp.PropertyType, destProp.PropertyType, sourceValue, out var nestedValue))
                {
                    destProp.SetValue(dest, nestedValue);
                }
                else if (destProp.PropertyType == sourceProp.PropertyType)
                {
                    destProp.SetValue(dest, sourceValue);
                }
            }
        }

        return dest;
    }
}