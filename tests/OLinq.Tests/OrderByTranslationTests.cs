using System.Linq.Expressions;
using FluentAssertions;
using OLinq.Translation;
using Xunit;

namespace OLinq.Tests;

public class OrderByTranslationTests
{
    [Fact]
    public void Ascending_StringProperty()
    {
        LambdaExpression lambda = (Expression<Func<TestEntity, string>>)(x => x.Name);
        ODataOrderByVisitor.Translate(lambda, descending: false).Should().Be("Name asc");
    }

    [Fact]
    public void Descending_IntProperty()
    {
        LambdaExpression lambda = (Expression<Func<TestEntity, int>>)(x => x.Age);
        ODataOrderByVisitor.Translate(lambda, descending: true).Should().Be("Age desc");
    }

    [Fact]
    public void Ascending_WithObjectReturn()
    {
        // When used with LINQ OrderBy<T, object> the expression has a Convert wrapper
        LambdaExpression lambda = (Expression<Func<TestEntity, object>>)(x => x.Name);
        ODataOrderByVisitor.Translate(lambda, descending: false).Should().Be("Name asc");
    }

    [Fact]
    public void NestedProperty()
    {
        LambdaExpression lambda = (Expression<Func<TestEntity, string>>)(x => x.Address.City);
        ODataOrderByVisitor.Translate(lambda, descending: false).Should().Be("Address/City asc");
    }

    [Fact]
    public void DateTimeProperty()
    {
        LambdaExpression lambda = (Expression<Func<TestEntity, DateTime>>)(x => x.CreatedAt);
        ODataOrderByVisitor.Translate(lambda, descending: true).Should().Be("CreatedAt desc");
    }
}
