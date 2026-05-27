using System.Collections;
using System.Linq.Expressions;
using OLinq.Models;

namespace OLinq;

/// <summary>An IQueryable that translates LINQ expressions to OData query strings.</summary>
public sealed class ODataQueryable<T> : IOrderedQueryable<T>
{
    private readonly HttpClient _httpClient;
    private readonly string _entitySetUrl;
    private readonly ODataQueryProvider _provider;

    internal ODataQueryable(HttpClient httpClient, string entitySetUrl, ODataQueryProvider provider, Expression expression)
    {
        _httpClient = httpClient;
        _entitySetUrl = entitySetUrl;
        _provider = provider;
        Expression = expression;
    }

    internal ODataQueryable(HttpClient httpClient, string entitySetUrl)
    {
        _httpClient = httpClient;
        _entitySetUrl = entitySetUrl;
        _provider = new ODataQueryProvider(httpClient, entitySetUrl);
        Expression = Expression.Constant(this);
    }

    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider => _provider;

    /// <summary>Executes the query and returns an ODataCollection with pagination metadata.</summary>
    public Task<ODataCollection<T>> ToCollectionAsync(CancellationToken cancellationToken = default) =>
        _provider.ExecuteQueryAsync<T>(Expression, cancellationToken);

    /// <summary>Executes the query and returns just the items list.</summary>
    public async Task<IReadOnlyList<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var collection = await _provider.ExecuteQueryAsync<T>(Expression, cancellationToken);
        return collection.Items;
    }

    /// <summary>Returns the OData URL that this query would produce.</summary>
    public string ToODataUrl() => _provider.BuildQueryUrl(Expression);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        throw new NotSupportedException("ODataQueryable is async-only. Use ToListAsync() or ToCollectionAsync().");

    IEnumerator IEnumerable.GetEnumerator() =>
        throw new NotSupportedException("ODataQueryable is async-only. Use ToListAsync() or ToCollectionAsync().");
}
