namespace OLinq;

internal sealed class ODataQueryContext
{
    public string? Filter { get; set; }
    public string? Select { get; set; }
    public List<string> OrderByParts { get; } = [];
    public int? Top { get; set; }
    public int? Skip { get; set; }
    public bool IncludeCount { get; set; }
    public List<string> ExpandParts { get; } = [];
    public string? Search { get; set; }
    public string? Apply { get; set; }

    public string BuildQueryString()
    {
        var parts = new List<string>();

        if (Filter is not null)
            parts.Add($"$filter={Uri.EscapeDataString(Filter)}");

        if (Select is not null)
            parts.Add($"$select={Select}");

        if (OrderByParts.Count > 0)
            parts.Add($"$orderby={Uri.EscapeDataString(string.Join(",", OrderByParts))}");

        if (Top.HasValue)
            parts.Add($"$top={Top.Value}");

        if (Skip.HasValue)
            parts.Add($"$skip={Skip.Value}");

        if (IncludeCount)
            parts.Add("$count=true");

        if (ExpandParts.Count > 0)
            parts.Add($"$expand={string.Join(",", ExpandParts)}");

        if (Search is not null)
            parts.Add($"$search={Uri.EscapeDataString(Search)}");

        if (Apply is not null)
            parts.Add($"$apply={Uri.EscapeDataString(Apply)}");

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }
}
