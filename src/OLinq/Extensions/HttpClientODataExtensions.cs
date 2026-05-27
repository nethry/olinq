namespace OLinq.Extensions;

/// <summary>Convenience extensions for creating OData queryables from HttpClient.</summary>
public static class HttpClientODataExtensions
{
    /// <summary>Creates an ODataQueryable targeting the given entity set URL.</summary>
    public static ODataQueryable<T> ODataQuery<T>(this HttpClient client, string entitySetUrl)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(entitySetUrl);

        return new ODataQueryable<T>(client, entitySetUrl);
    }
}
