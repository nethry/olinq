namespace OLinq.Tests;

internal class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public double Score { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Address Address { get; set; } = new();
    public List<string> Tags { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public int? OptionalAge { get; set; }
}

internal class Address
{
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

internal class Order
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}
