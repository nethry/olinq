using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Text.Json;
using OLinq.Models;
using OLinq.Translation;

namespace OLinq;

internal sealed class ODataQueryProvider : IQueryProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _entitySetUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public ODataQueryProvider(HttpClient httpClient, string entitySetUrl, JsonSerializerOptions? jsonOptions = null)
    {
        _httpClient = httpClient;
        _entitySetUrl = entitySetUrl;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public IQueryable CreateQuery(Expression expression) =>
        throw new NotSupportedException("Non-generic CreateQuery is not supported.");

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new ODataQueryable<TElement>(_httpClient, _entitySetUrl, this, expression);

    public object? Execute(Expression expression) =>
        throw new NotSupportedException("Non-generic Execute is not supported. Use async methods.");

    public TResult Execute<TResult>(Expression expression) =>
        throw new NotSupportedException("Use ExecuteAsync instead of synchronous Execute.");

    internal async Task<ODataCollection<T>> ExecuteQueryAsync<T>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        var context = ODataExpressionTranslator.Translate(expression);
        var queryString = context.BuildQueryString();
        var url = _entitySetUrl + queryString;

        var response = await _httpClient.GetFromJsonAsync<ODataResponse<T>>(url, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"OData request to '{url}' returned null.");

        return new ODataCollection<T>
        {
            Items = response.Value,
            TotalCount = response.Count,
            NextLink = response.NextLink
        };
    }

    internal async Task<T?> ExecuteSingleAsync<T>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        var context = ODataExpressionTranslator.Translate(expression);
        var queryString = context.BuildQueryString();
        var url = _entitySetUrl + queryString;

        return await _httpClient.GetFromJsonAsync<T>(url, _jsonOptions, cancellationToken);
    }

    internal string BuildQueryUrl(Expression expression)
    {
        var context = ODataExpressionTranslator.Translate(expression);
        return _entitySetUrl + context.BuildQueryString();
    }
}
