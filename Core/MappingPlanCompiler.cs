using AutoMapperLite.Interfaces;
using System.Linq.Expressions;
using System.Reflection;

namespace AutoMapperLite
{
    /// <summary>
    /// Compiles a <see cref="MapBuilder{TSource, TDestination}"/>'s configuration into a real
    /// delegate via <see cref="System.Linq.Expressions"/>, once per builder instance (lazily, on
    /// first use — see <see cref="MapBuilder{TSource, TDestination}.Map"/>). This replaces what
    /// used to be an interpreted, per-property reflection walk (<c>PropertyInfo.GetValue</c>/
    /// <c>SetValue</c>, <c>MethodInfo.Invoke</c>) on every single mapped object with a compiled
    /// delegate that performs direct property access, invoked exactly like a normal method call.
    /// See <c>CLAUDE.md</c> and <c>docs/performance.html</c> for the measured impact and the
    /// parts of the pipeline (ForPath nested-path walking) that remain deliberately interpreted.
    /// </summary>
    internal static class MappingPlanCompiler
    {
        private static readonly MethodInfo GetMapMethodDefinition =
            typeof(IMapperConfig).GetMethod(nameof(IMapperConfig.GetMap))!;

        private static readonly MethodInfo MapListWithBuilderDefinition =
            typeof(MappingPlanCompiler).GetMethod(nameof(MapListWithBuilder), BindingFlags.NonPublic | BindingFlags.Static)!;

        private static readonly MethodInfo MapListToArrayWithBuilderDefinition =
            typeof(MappingPlanCompiler).GetMethod(nameof(MapListToArrayWithBuilder), BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static Func<TSource, TDestination> Compile<TSource, TDestination>(
            MapBuilder<TSource, TDestination> builder, IMapperConfig config)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "source");
            var destVar = Expression.Variable(typeof(TDestination), "dest");

            var body = new List<Expression>
            {
                Expression.Assign(destVar, CreateInstanceExpression(typeof(TDestination))),
            };

            var sourcePropsByName = Mapper.GetPropertiesByName(typeof(TSource));

            foreach (var prop in Mapper.GetPublicProperties(typeof(TDestination)))
            {
                if (!prop.CanWrite) continue;

                if (builder.MemberMappings.TryGetValue(prop.Name, out var customMap))
                {
                    body.Add(BuildForMemberAssignment(destVar, prop, customMap, prop.Name, sourceParam));
                    continue;
                }

                var nestedKeys = builder.GetNestedKeys(prop.Name);
                if (nestedKeys.Count > 0)
                {
                    body.Add(BuildNestedAssignment(destVar, prop, nestedKeys, builder, sourceParam));
                    continue;
                }

                if (!sourcePropsByName.TryGetValue(prop.Name, out var sourceProp) || !sourceProp.CanRead)
                    continue;

                body.Add(BuildAutoMapAssignment(destVar, prop, sourceProp, sourceParam, config));
            }

            body.Add(destVar);

            var block = Expression.Block(new[] { destVar }, body);
            return Expression.Lambda<Func<TSource, TDestination>>(block, sourceParam).Compile();
        }

        private static Expression BuildForMemberAssignment<TSource>(
            ParameterExpression destVar, PropertyInfo prop, Func<TSource, object?> customMap,
            string propertyPath, ParameterExpression sourceParam)
        {
            var mapConst = Expression.Constant(customMap, typeof(Func<TSource, object?>));
            var rawValue = Expression.Invoke(mapConst, sourceParam);
            var convertMethod = Mapper.ConvertMappedValueDefinition.MakeGenericMethod(prop.PropertyType);
            var converted = Expression.Call(convertMethod, rawValue, Expression.Constant(propertyPath));
            return Expression.Assign(Expression.Property(destVar, prop), converted);
        }

        private static Expression BuildNestedAssignment<TSource, TDestination>(
            ParameterExpression destVar, PropertyInfo prop, IReadOnlyList<string> nestedKeys,
            MapBuilder<TSource, TDestination> builder, ParameterExpression sourceParam)
        {
            var nestedVar = Expression.Variable(prop.PropertyType, "nested");
            var applyMethod = Mapper.ApplyNestedMappingDefinition.MakeGenericMethod(typeof(TSource), typeof(TDestination));
            var builderConst = Expression.Constant(builder, typeof(MapBuilder<TSource, TDestination>));

            var exprs = new List<Expression>
            {
                Expression.Assign(nestedVar, CreateInstanceExpression(prop.PropertyType)),
            };

            foreach (var key in nestedKeys)
            {
                exprs.Add(Expression.Call(
                    applyMethod,
                    Expression.Convert(nestedVar, typeof(object)),
                    sourceParam,
                    Expression.Constant(key),
                    builderConst));
            }

            exprs.Add(Expression.Assign(Expression.Property(destVar, prop), nestedVar));

            return Expression.Block(new[] { nestedVar }, exprs);
        }

        private static Expression BuildAutoMapAssignment(
            ParameterExpression destVar, PropertyInfo destProp, PropertyInfo sourceProp,
            ParameterExpression sourceParam, IMapperConfig config)
        {
            var sourceValue = Expression.Property(sourceParam, sourceProp);
            var destAccess = Expression.Property(destVar, destProp);

            Expression assign;
            if (destProp.PropertyType == sourceProp.PropertyType)
            {
                assign = Expression.Assign(destAccess, sourceValue);
            }
            else
            {
                // Type mismatch: only map if a matching type map is registered, either directly,
                // or (for List<T> properties) between the two item types — resolved once here,
                // at plan-build time, not re-checked per call. In both cases the nested
                // MapBuilder itself is also resolved here, at compile time, and baked into the
                // compiled expression as a constant — see BuildNestedObjectAssignment/
                // BuildNestedListAssignment/BuildNestedArrayAssignment for why: it removes
                // source.GetType() and a dictionary lookup from the runtime path entirely.
                if (config.HasMap(sourceProp.PropertyType, destProp.PropertyType))
                {
                    assign = BuildNestedObjectAssignment(destAccess, sourceValue, sourceProp.PropertyType, destProp.PropertyType, config);
                }
                else if (Mapper.TryGetListItemTypes(sourceProp.PropertyType, destProp.PropertyType, out var itemSource, out var itemDest)
                         && config.HasMap(itemSource, itemDest))
                {
                    assign = BuildNestedListAssignment(destAccess, sourceValue, itemSource, itemDest, config);
                }
                else if (Mapper.TryGetListToArrayItemTypes(sourceProp.PropertyType, destProp.PropertyType, out var arrItemSource, out var arrItemDest)
                         && config.HasMap(arrItemSource, arrItemDest))
                {
                    assign = BuildNestedArrayAssignment(destAccess, sourceValue, arrItemSource, arrItemDest, config);
                }
                else
                {
                    return Expression.Empty(); // no registered map for this type pair — skip
                }
            }

            if (!CanBeNull(sourceProp.PropertyType)) return assign;

            // A null source value is skipped, not assigned — preserves the destination's own
            // default instead of overwriting it with null.
            return Expression.IfThen(
                Expression.NotEqual(sourceValue, Expression.Constant(null, sourceProp.PropertyType)),
                assign);
        }

        // Resolves the nested MapBuilder ONCE, here at plan-compile time (not per call, and not
        // via source.GetType() at all), and bakes the actual builder reference into the compiled
        // expression as a constant. Previously this routed through Mapper.MapCore<TDestination>
        // (object, config) at runtime, which needed the source value's runtime type plus a
        // dictionary lookup on every single call to find the nested builder — despite the exact
        // nested type pair already being known here, at compile time, via the HasMap check above.
        // The compiled parent delegate now calls straight into the already-known nested builder's
        // own (also lazily-compiled) delegate: zero runtime type resolution, zero dictionary
        // lookup, zero boxing of the nested value.
        private static Expression BuildNestedObjectAssignment(
            MemberExpression destAccess, Expression sourceValue, Type nestedSourceType, Type nestedDestType, IMapperConfig config)
        {
            var builder = GetMapMethodDefinition.MakeGenericMethod(nestedSourceType, nestedDestType).Invoke(config, null)!;
            var builderType = typeof(MapBuilder<,>).MakeGenericType(nestedSourceType, nestedDestType);
            var builderConst = Expression.Constant(builder, builderType);
            var mapMethod = builderType.GetMethod("Map", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var configConst = Expression.Constant(config, typeof(IMapperConfig));
            var mapped = Expression.Call(builderConst, mapMethod, sourceValue, configConst);
            return Expression.Assign(destAccess, mapped);
        }

        // Same idea as BuildNestedObjectAssignment, for a nested List<TSourceItem> ->
        // List<TDestItem> property whose item types have their own registered map: the item
        // builder is resolved once here, at compile time, and the compiled parent delegate calls
        // a strongly-typed loop (MapListWithBuilder below) that maps every element with zero
        // per-item GetType()/dictionary lookup/boxing — the same fix applied to top-level
        // collection mapping in Core/Mapper.cs, applied here to nested collection properties.
        private static Expression BuildNestedListAssignment(
            MemberExpression destAccess, Expression sourceValue, Type itemSourceType, Type itemDestType, IMapperConfig config)
        {
            var builder = GetMapMethodDefinition.MakeGenericMethod(itemSourceType, itemDestType).Invoke(config, null)!;
            var builderType = typeof(MapBuilder<,>).MakeGenericType(itemSourceType, itemDestType);
            var builderConst = Expression.Constant(builder, builderType);
            var configConst = Expression.Constant(config, typeof(IMapperConfig));
            var loopMethod = MapListWithBuilderDefinition.MakeGenericMethod(itemSourceType, itemDestType);
            var mapped = Expression.Call(loopMethod, sourceValue, builderConst, configConst);
            return Expression.Assign(destAccess, mapped);
        }

        private static List<TDestItem> MapListWithBuilder<TSourceItem, TDestItem>(
            List<TSourceItem> source, MapBuilder<TSourceItem, TDestItem> builder, IMapperConfig config)
        {
            // Resolves the compiled delegate once per call (i.e. once per nested list, not once
            // per element) - same fixed-per-item-cost fix as Mapper.BuildListMapper, applied to
            // nested List<T> properties. Still triggered from a real runtime Map call already in
            // progress (this method itself is only ever invoked from the parent's own compiled
            // delegate while it executes), never from another type's plan-compilation, so it
            // carries no eager-recursion risk for self-referential item types.
            var compiled = builder.GetCompiledMap(config);
            var result = new List<TDestItem>(source.Count);
            foreach (var item in source)
                result.Add(compiled(item));
            return result;
        }

        // Same idea as BuildNestedListAssignment, for a nested List<TSourceItem> -> TDestItem[]
        // property (source stays List<T>, destination is an array) whose item types have their
        // own registered map. A separate helper is needed rather than widening
        // BuildNestedListAssignment/MapListWithBuilder, because List<TDestItem> is not
        // reference-assignable to TDestItem[] - see Mapper.TryGetListToArrayItemTypes.
        private static Expression BuildNestedArrayAssignment(
            MemberExpression destAccess, Expression sourceValue, Type itemSourceType, Type itemDestType, IMapperConfig config)
        {
            var builder = GetMapMethodDefinition.MakeGenericMethod(itemSourceType, itemDestType).Invoke(config, null)!;
            var builderType = typeof(MapBuilder<,>).MakeGenericType(itemSourceType, itemDestType);
            var builderConst = Expression.Constant(builder, builderType);
            var configConst = Expression.Constant(config, typeof(IMapperConfig));
            var loopMethod = MapListToArrayWithBuilderDefinition.MakeGenericMethod(itemSourceType, itemDestType);
            var mapped = Expression.Call(loopMethod, sourceValue, builderConst, configConst);
            return Expression.Assign(destAccess, mapped);
        }

        private static TDestItem[] MapListToArrayWithBuilder<TSourceItem, TDestItem>(
            List<TSourceItem> source, MapBuilder<TSourceItem, TDestItem> builder, IMapperConfig config)
        {
            var compiled = builder.GetCompiledMap(config);
            var result = new TDestItem[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = compiled(source[i]);
            return result;
        }

        private static bool CanBeNull(Type type) =>
            !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

        private static NewExpression CreateInstanceExpression(Type type)
        {
            // Expression.New(Type) — not GetConstructor(Type.EmptyTypes) — is deliberate: value
            // types never report a reflectable parameterless ConstructorInfo unless one is
            // explicitly declared (a well-known reflection gotcha), even though they always
            // support default-construction. Expression.New(Type) handles that case correctly on
            // its own and only throws for reference types genuinely missing a parameterless ctor.
            try
            {
                return Expression.New(type);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"Cannot map to '{type.Name}': it has no public parameterless constructor. " +
                    "AutoMapperLite creates destination instances via a compiled constructor call, so " +
                    "positional records and classes requiring constructor arguments are not supported " +
                    "as mapping destinations — use a type with a public parameterless constructor " +
                    "(init-only properties are fine, e.g. a record with { get; init; } members).", ex);
            }
        }
    }
}
