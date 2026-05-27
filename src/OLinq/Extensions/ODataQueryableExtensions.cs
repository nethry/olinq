using System.Linq.Expressions;
using System.Reflection;

namespace OLinq.Extensions;

/// <summary>
/// OData-specific LINQ extension methods that have no standard LINQ equivalent.
/// These encode OData-only query options ($expand, $search, $count, $apply).
/// </summary>
public static class ODataQueryableExtensions
{
    /// <summary>
    /// Returns the OData URL that the current queryable expression would produce.
    /// Works on any IQueryable backed by an ODataQueryProvider.
    /// </summary>
    public static string ToODataUrl<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is ODataQueryProvider provider)
            return provider.BuildQueryUrl(source.Expression);

        if (source is ODataQueryable<T> oq)
            return oq.ToODataUrl();

        throw new InvalidOperationException(
            "ToODataUrl() can only be called on an IQueryable backed by ODataQueryProvider.");
    }

    /// <summary>
    /// Executes the OData query asynchronously and returns all items.
    /// </summary>
    public static Task<IReadOnlyList<T>> ToListAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is ODataQueryable<T> oq)
            return oq.ToListAsync(cancellationToken);

        if (source.Provider is ODataQueryProvider provider)
            return ExecuteAsListAsync<T>(provider, source.Expression, cancellationToken);

        throw new InvalidOperationException(
            "ToListAsync() can only be called on an IQueryable backed by ODataQueryProvider.");
    }

    /// <summary>
    /// Executes the OData query asynchronously and returns a collection with pagination metadata.
    /// </summary>
    public static Task<OLinq.Models.ODataCollection<T>> ToCollectionAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is ODataQueryable<T> oq)
            return oq.ToCollectionAsync(cancellationToken);

        if (source.Provider is ODataQueryProvider provider)
            return provider.ExecuteQueryAsync<T>(source.Expression, cancellationToken);

        throw new InvalidOperationException(
            "ToCollectionAsync() can only be called on an IQueryable backed by ODataQueryProvider.");
    }

    private static async Task<IReadOnlyList<T>> ExecuteAsListAsync<T>(
        ODataQueryProvider provider,
        Expression expression,
        CancellationToken cancellationToken)
    {
        var collection = await provider.ExecuteQueryAsync<T>(expression, cancellationToken);
        return collection.Items;
    }

    private static readonly MethodInfo s_expandMethod =
        typeof(ODataQueryableExtensions).GetMethod(nameof(Expand))!;
    private static readonly MethodInfo s_searchMethod =
        typeof(ODataQueryableExtensions).GetMethod(nameof(Search))!;
    private static readonly MethodInfo s_withCountMethod =
        typeof(ODataQueryableExtensions).GetMethod(nameof(WithCount))!;
    private static readonly MethodInfo s_applyMethod =
        typeof(ODataQueryableExtensions).GetMethod(nameof(Apply))!;
    private static readonly MethodInfo s_expandNestedMethod =
        typeof(ODataQueryableExtensions).GetMethod(nameof(ExpandNested))!;

    /// <summary>Adds an $expand clause for a navigation property.</summary>
    public static IQueryable<T> Expand<T, TProperty>(
        this IQueryable<T> source,
        Expression<Func<T, TProperty>> navigationProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationProperty);

        return source.Provider.CreateQuery<T>(
            Expression.Call(
                null,
                s_expandMethod.MakeGenericMethod(typeof(T), typeof(TProperty)),
                source.Expression,
                Expression.Quote(navigationProperty)));
    }

    /// <summary>Adds a $search clause.</summary>
    public static IQueryable<T> Search<T>(this IQueryable<T> source, string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(searchTerm);

        return source.Provider.CreateQuery<T>(
            Expression.Call(
                null,
                s_searchMethod.MakeGenericMethod(typeof(T)),
                source.Expression,
                Expression.Constant(searchTerm)));
    }

    /// <summary>Includes $count=true in the request to retrieve the total item count.</summary>
    public static IQueryable<T> WithCount<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Provider.CreateQuery<T>(
            Expression.Call(
                null,
                s_withCountMethod.MakeGenericMethod(typeof(T)),
                source.Expression));
    }

    /// <summary>Adds a raw $apply clause for aggregations (groupby, aggregate, etc.).</summary>
    public static IQueryable<T> Apply<T>(this IQueryable<T> source, string applyExpression)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(applyExpression);

        return source.Provider.CreateQuery<T>(
            Expression.Call(
                null,
                s_applyMethod.MakeGenericMethod(typeof(T)),
                source.Expression,
                Expression.Constant(applyExpression)));
    }

    /// <summary>
    /// Expands a navigation property with nested query options.
    /// Example: .ExpandNested(x => x.Orders, q => q.Select(o => new { o.Amount }).Take(5))
    /// </summary>
    public static IQueryable<T> ExpandNested<T, TProperty>(
        this IQueryable<T> source,
        Expression<Func<T, IEnumerable<TProperty>>> navigationProperty,
        Expression<Func<IQueryable<TProperty>, IQueryable<TProperty>>> nestedQuery)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationProperty);
        ArgumentNullException.ThrowIfNull(nestedQuery);

        return source.Provider.CreateQuery<T>(
            Expression.Call(
                null,
                s_expandNestedMethod.MakeGenericMethod(typeof(T), typeof(TProperty)),
                source.Expression,
                Expression.Quote(navigationProperty),
                Expression.Quote(nestedQuery)));
    }
}
