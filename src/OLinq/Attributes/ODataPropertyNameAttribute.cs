namespace OLinq.Attributes;

/// <summary>Overrides the OData property name for a CLR property.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ODataPropertyNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
