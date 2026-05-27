using FluentAssertions;
using OLinq;
using OLinq.Extensions;
using Xunit;

namespace OLinq.Tests;

public class QueryBuildingTests
{
    private static ODataQueryable<TestEntity> CreateQueryable(string baseUrl = "https://api.example.com/odata/Entities")
    {
        // Use a simple handler stub - we only test URL generation, not actual HTTP
        var client = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        return client.ODataQuery<TestEntity>(baseUrl);
    }

    [Fact]
    public void NoFilter_BaseUrl()
    {
        var q = CreateQueryable();
        q.ToODataUrl().Should().Be("https://api.example.com/odata/Entities");
    }

    [Fact]
    public void Where_GeneratesFilter()
    {
        var q = CreateQueryable().Where(x => x.Name == "Alice");
        var url = q.ToODataUrl();
        url.Should().Contain("$filter=");
        // Uri.EscapeDataString encodes spaces as %20 and single quotes as %27
        url.Should().Contain("Name%20eq%20%27Alice%27");
    }

    [Fact]
    public void Take_GeneratesTop()
    {
        var q = CreateQueryable().Take(10);
        q.ToODataUrl().Should().Contain("$top=10");
    }

    [Fact]
    public void Skip_GeneratesSkip()
    {
        var q = CreateQueryable().Skip(5);
        q.ToODataUrl().Should().Contain("$skip=5");
    }

    [Fact]
    public void OrderBy_GeneratesAsc()
    {
        var q = CreateQueryable().OrderBy(x => x.Name);
        q.ToODataUrl().Should().Contain("$orderby=Name%20asc");
    }

    [Fact]
    public void OrderByDescending_GeneratesDesc()
    {
        var q = CreateQueryable().OrderByDescending(x => x.Age);
        q.ToODataUrl().Should().Contain("Age%20desc");
    }

    [Fact]
    public void ThenBy_CombinesOrderBy()
    {
        var q = CreateQueryable()
            .OrderBy(x => x.Name)
            .ThenByDescending(x => x.Age);
        var url = q.ToODataUrl();
        url.Should().Contain("Name%20asc");
        url.Should().Contain("Age%20desc");
    }

    [Fact]
    public void Select_AnonymousType_GeneratesSelectClause()
    {
        var q = CreateQueryable().Select(x => new { x.Name, x.Age });
        q.ToODataUrl().Should().Contain("$select=Name,Age");
    }

    [Fact]
    public void Expand_NavigationProperty()
    {
        var q = CreateQueryable().Expand(x => x.Address);
        q.ToODataUrl().Should().Contain("$expand=Address");
    }

    [Fact]
    public void Expand_Multiple()
    {
        var q = CreateQueryable()
            .Expand(x => x.Address)
            .Expand(x => x.Orders);
        var url = q.ToODataUrl();
        url.Should().Contain("Address");
        url.Should().Contain("Orders");
    }

    [Fact]
    public void Search_GeneratesSearchClause()
    {
        var q = CreateQueryable().Search("Alice");
        q.ToODataUrl().Should().Contain("$search=Alice");
    }

    [Fact]
    public void WithCount_GeneratesCountTrue()
    {
        var q = CreateQueryable().WithCount();
        q.ToODataUrl().Should().Contain("$count=true");
    }

    [Fact]
    public void Apply_GeneratesApplyClause()
    {
        var q = CreateQueryable().Apply("groupby((Name),aggregate(Age with average as AvgAge))");
        q.ToODataUrl().Should().Contain("$apply=");
    }

    [Fact]
    public void MultipleWhere_CombinedWithAnd()
    {
        var q = CreateQueryable()
            .Where(x => x.Age > 18)
            .Where(x => x.IsActive);
        var url = q.ToODataUrl();
        url.Should().Contain("$filter=");
        url.Should().Contain("and");
    }

    [Fact]
    public void CombinedQuery_AllOptions()
    {
        var q = CreateQueryable()
            .Where(x => x.Age > 18)
            .OrderBy(x => x.Name)
            .Skip(10)
            .Take(5)
            .WithCount();

        var url = q.ToODataUrl();
        url.Should().Contain("$filter=");
        url.Should().Contain("$orderby=");
        url.Should().Contain("$skip=10");
        url.Should().Contain("$top=5");
        url.Should().Contain("$count=true");
    }

    [Fact]
    public void ToODataUrl_ReturnsParsableUri()
    {
        var q = CreateQueryable()
            .Where(x => x.Name == "Alice")
            .Take(10);

        var url = q.ToODataUrl();
        var act = () => new Uri(url);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetEnumerator_ThrowsNotSupported()
    {
        var q = CreateQueryable();
        var act = () => { foreach (var _ in q) { } };
        act.Should().Throw<NotSupportedException>();
    }
}
