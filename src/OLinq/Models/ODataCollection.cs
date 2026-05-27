namespace OLinq.Models;

public class ODataCollection<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public long? TotalCount { get; init; }
    public string? NextLink { get; init; }
}
