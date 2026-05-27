using FluentAssertions;
using Xunit;

namespace OLinq.Tests;

public class ODataQueryContextTests
{
    [Fact]
    public void Empty_ReturnsEmptyString()
    {
        var ctx = new ODataQueryContext();
        ctx.BuildQueryString().Should().BeEmpty();
    }

    [Fact]
    public void Filter_UrlEncoded()
    {
        var ctx = new ODataQueryContext { Filter = "Name eq 'Alice'" };
        // Uri.EscapeDataString encodes spaces as %20 and single quotes as %27
        ctx.BuildQueryString().Should().Be("?$filter=Name%20eq%20%27Alice%27");
    }

    [Fact]
    public void Top_Only()
    {
        var ctx = new ODataQueryContext { Top = 10 };
        ctx.BuildQueryString().Should().Be("?$top=10");
    }

    [Fact]
    public void Skip_Only()
    {
        var ctx = new ODataQueryContext { Skip = 5 };
        ctx.BuildQueryString().Should().Be("?$skip=5");
    }

    [Fact]
    public void TopAndSkip()
    {
        var ctx = new ODataQueryContext { Top = 10, Skip = 5 };
        ctx.BuildQueryString().Should().Be("?$top=10&$skip=5");
    }

    [Fact]
    public void Select_NotEncoded()
    {
        var ctx = new ODataQueryContext { Select = "Name,Age" };
        ctx.BuildQueryString().Should().Be("?$select=Name,Age");
    }

    [Fact]
    public void OrderBy_SinglePart()
    {
        var ctx = new ODataQueryContext();
        ctx.OrderByParts.Add("Name asc");
        ctx.BuildQueryString().Should().Be("?$orderby=Name%20asc");
    }

    [Fact]
    public void OrderBy_MultipleParts()
    {
        var ctx = new ODataQueryContext();
        ctx.OrderByParts.Add("Name asc");
        ctx.OrderByParts.Add("Age desc");
        ctx.BuildQueryString().Should().Be("?$orderby=Name%20asc%2CAge%20desc");
    }

    [Fact]
    public void IncludeCount()
    {
        var ctx = new ODataQueryContext { IncludeCount = true };
        ctx.BuildQueryString().Should().Be("?$count=true");
    }

    [Fact]
    public void Expand_Single()
    {
        var ctx = new ODataQueryContext();
        ctx.ExpandParts.Add("Orders");
        ctx.BuildQueryString().Should().Be("?$expand=Orders");
    }

    [Fact]
    public void Expand_Multiple()
    {
        var ctx = new ODataQueryContext();
        ctx.ExpandParts.Add("Orders");
        ctx.ExpandParts.Add("Address");
        ctx.BuildQueryString().Should().Be("?$expand=Orders,Address");
    }

    [Fact]
    public void Search()
    {
        var ctx = new ODataQueryContext { Search = "hello world" };
        ctx.BuildQueryString().Should().Be("?$search=hello%20world");
    }

    [Fact]
    public void AllOptions_Combined()
    {
        var ctx = new ODataQueryContext
        {
            Filter = "Age gt 18",
            Select = "Name,Age",
            Top = 10,
            Skip = 0,
            IncludeCount = true
        };
        ctx.OrderByParts.Add("Name asc");
        ctx.ExpandParts.Add("Orders");

        var qs = ctx.BuildQueryString();
        qs.Should().StartWith("?");
        qs.Should().Contain("$filter=");
        qs.Should().Contain("$select=Name,Age");
        qs.Should().Contain("$orderby=");
        qs.Should().Contain("$top=10");
        qs.Should().Contain("$skip=0");
        qs.Should().Contain("$count=true");
        qs.Should().Contain("$expand=Orders");
    }
}
