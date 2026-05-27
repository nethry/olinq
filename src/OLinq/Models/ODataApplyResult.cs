using System.Text.Json.Serialization;

namespace OLinq.Models;

public class ODataApplyResult
{
    [JsonPropertyName("@odata.context")]
    public string? Context { get; set; }

    [JsonPropertyName("value")]
    public List<Dictionary<string, object?>> Value { get; set; } = [];
}
