using System.Linq.Expressions;
using FluentAssertions;
using OLinq.Translation;
using Xunit;

namespace OLinq.Tests;

public class SelectTranslationTests
{
    private static string Translate<T, TResult>(Expression<Func<T, TResult>> selector) =>
        ODataSelectVisitor.Translate(selector);

    [Fact]
    public void SingleStringProperty()
    {
        var result = Translate<TestEntity, string>(x => x.Name);
        result.Should().Be("Name");
    }

    [Fact]
    public void SingleIntProperty()
    {
        var result = Translate<TestEntity, int>(x => x.Age);
        result.Should().Be("Age");
    }

    [Fact]
    public void AnonymousProjection_TwoProperties()
    {
        LambdaExpression lambda = (Expression<Func<TestEntity, object>>)(x => new { x.Name, x.Age });
        var result = ODataSelectVisitor.Translate(lambda);
        result.Should().Be("Name,Age");
    }

    [Fact]
    public void AnonymousProjection_ThreeProperties()
    {
        LambdaExpression lambda = (Expression<Func<TestEntity, object>>)(x => new { x.Id, x.Name, x.Age });
        var result = ODataSelectVisitor.Translate(lambda);
        result.Should().Be("Id,Name,Age");
    }

    [Fact]
    public void NestedProperty()
    {
        var result = Translate<TestEntity, string>(x => x.Address.City);
        result.Should().Be("Address/City");
    }
}
