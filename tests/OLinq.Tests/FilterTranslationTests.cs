using System.Linq.Expressions;
using FluentAssertions;
using OLinq.Translation;
using Xunit;

namespace OLinq.Tests;

public class FilterTranslationTests
{
    private static string Translate<T>(Expression<Func<T, bool>> predicate) =>
        ODataFilterVisitor.Translate(predicate.Body);

    // --- Basic comparisons ---

    [Fact]
    public void Equal_StringProperty() =>
        Translate<TestEntity>(x => x.Name == "Alice").Should().Be("Name eq 'Alice'");

    [Fact]
    public void Equal_IntProperty() =>
        Translate<TestEntity>(x => x.Age == 30).Should().Be("Age eq 30");

    [Fact]
    public void NotEqual() =>
        Translate<TestEntity>(x => x.Name != "Alice").Should().Be("Name ne 'Alice'");

    [Fact]
    public void GreaterThan() =>
        Translate<TestEntity>(x => x.Age > 18).Should().Be("Age gt 18");

    [Fact]
    public void GreaterThanOrEqual() =>
        Translate<TestEntity>(x => x.Age >= 18).Should().Be("Age ge 18");

    [Fact]
    public void LessThan() =>
        Translate<TestEntity>(x => x.Age < 65).Should().Be("Age lt 65");

    [Fact]
    public void LessThanOrEqual() =>
        Translate<TestEntity>(x => x.Age <= 65).Should().Be("Age le 65");

    // --- Logical operators ---

    [Fact]
    public void AndAlso() =>
        Translate<TestEntity>(x => x.Name == "Alice" && x.Age > 18)
            .Should().Be("(Name eq 'Alice' and Age gt 18)");

    [Fact]
    public void OrElse() =>
        Translate<TestEntity>(x => x.Name == "Alice" || x.Name == "Bob")
            .Should().Be("(Name eq 'Alice' or Name eq 'Bob')");

    [Fact]
    public void Not() =>
        Translate<TestEntity>(x => !x.IsActive)
            .Should().Be("not (IsActive)");

    // --- Null checks ---

    [Fact]
    public void EqualNull() =>
        Translate<TestEntity>(x => x.Name == null).Should().Be("Name eq null");

    [Fact]
    public void NotEqualNull() =>
        Translate<TestEntity>(x => x.Name != null).Should().Be("Name ne null");

    // --- String functions ---

    [Fact]
    public void Contains() =>
        Translate<TestEntity>(x => x.Name.Contains("Al")).Should().Be("contains(Name, 'Al')");

    [Fact]
    public void StartsWith() =>
        Translate<TestEntity>(x => x.Name.StartsWith("Al")).Should().Be("startswith(Name, 'Al')");

    [Fact]
    public void EndsWith() =>
        Translate<TestEntity>(x => x.Name.EndsWith("ce")).Should().Be("endswith(Name, 'ce')");

    [Fact]
    public void ToLower() =>
        Translate<TestEntity>(x => x.Name.ToLower() == "alice").Should().Be("tolower(Name) eq 'alice'");

    [Fact]
    public void ToUpper() =>
        Translate<TestEntity>(x => x.Name.ToUpper() == "ALICE").Should().Be("toupper(Name) eq 'ALICE'");

    [Fact]
    public void Trim() =>
        Translate<TestEntity>(x => x.Name.Trim() == "Alice").Should().Be("trim(Name) eq 'Alice'");

    [Fact]
    public void IndexOf() =>
        Translate<TestEntity>(x => x.Name.IndexOf("l") == 1).Should().Be("indexof(Name, 'l') eq 1");

    [Fact]
    public void Substring_OneArg() =>
        Translate<TestEntity>(x => x.Name.Substring(1) == "lice").Should().Be("substring(Name, 1) eq 'lice'");

    [Fact]
    public void Substring_TwoArgs() =>
        Translate<TestEntity>(x => x.Name.Substring(0, 3) == "Ali").Should().Be("substring(Name, 0, 3) eq 'Ali'");

    [Fact]
    public void StringLength() =>
        Translate<TestEntity>(x => x.Name.Length > 3).Should().Be("length(Name) gt 3");

    [Fact]
    public void StringConcat() =>
        Translate<TestEntity>(x => string.Concat(x.Name, " Jr") == "Alice Jr")
            .Should().Be("concat(Name, ' Jr') eq 'Alice Jr'");

    // --- Math functions ---

    [Fact]
    public void MathRound() =>
        Translate<TestEntity>(x => Math.Round(x.Score) == 5).Should().Be("round(Score) eq 5");

    [Fact]
    public void MathFloor() =>
        Translate<TestEntity>(x => Math.Floor(x.Score) == 4).Should().Be("floor(Score) eq 4");

    [Fact]
    public void MathCeiling() =>
        Translate<TestEntity>(x => Math.Ceiling(x.Score) == 5).Should().Be("ceiling(Score) eq 5");

    // --- DateTime functions ---

    [Fact]
    public void DateTimeYear() =>
        Translate<TestEntity>(x => x.CreatedAt.Year == 2024).Should().Be("year(CreatedAt) eq 2024");

    [Fact]
    public void DateTimeMonth() =>
        Translate<TestEntity>(x => x.CreatedAt.Month == 1).Should().Be("month(CreatedAt) eq 1");

    [Fact]
    public void DateTimeDay() =>
        Translate<TestEntity>(x => x.CreatedAt.Day == 15).Should().Be("day(CreatedAt) eq 15");

    // --- Captured variables ---

    [Fact]
    public void CapturedVariable()
    {
        var name = "Alice";
        Translate<TestEntity>(x => x.Name == name).Should().Be("Name eq 'Alice'");
    }

    [Fact]
    public void CapturedIntVariable()
    {
        var minAge = 18;
        Translate<TestEntity>(x => x.Age > minAge).Should().Be("Age gt 18");
    }

    // --- Collection 'in' operator ---

    [Fact]
    public void CollectionContains_In()
    {
        var ids = new[] { 1, 2, 3 };
        Translate<TestEntity>(x => ids.Contains(x.Id)).Should().Be("Id in (1, 2, 3)");
    }

    [Fact]
    public void ListContains_In()
    {
        var names = new List<string> { "Alice", "Bob" };
        Translate<TestEntity>(x => names.Contains(x.Name)).Should().Be("Name in ('Alice', 'Bob')");
    }

    // --- Any/All on collection navigation properties ---

    [Fact]
    public void AnyOnCollection() =>
        Translate<TestEntity>(x => x.Tags.Any(t => t == "important"))
            .Should().Be("Tags/any(t: t eq 'important')");

    [Fact]
    public void AllOnCollection() =>
        Translate<TestEntity>(x => x.Tags.All(t => t != "spam"))
            .Should().Be("Tags/all(t: t ne 'spam')");

    [Fact]
    public void AnyOnNavigationProperty() =>
        Translate<TestEntity>(x => x.Orders.Any(o => o.Amount > 100))
            .Should().Be("Orders/any(o: o/Amount gt 100M)");

    // --- Arithmetic operators ---

    [Fact]
    public void Add() =>
        Translate<TestEntity>(x => x.Age + 1 == 31).Should().Be("Age add 1 eq 31");

    [Fact]
    public void Multiply() =>
        Translate<TestEntity>(x => x.Age * 2 > 50).Should().Be("Age mul 2 gt 50");

    // --- Boolean property ---

    [Fact]
    public void BoolProperty() =>
        Translate<TestEntity>(x => x.IsActive).Should().Be("IsActive");

    // --- Nested navigation property ---

    [Fact]
    public void NestedProperty() =>
        Translate<TestEntity>(x => x.Address.City == "London").Should().Be("Address/City eq 'London'");

    // --- Nullable ---

    [Fact]
    public void NullableHasValue() =>
        Translate<TestEntity>(x => x.OptionalAge.HasValue).Should().Be("OptionalAge ne null");

    [Fact]
    public void NullableValue() =>
        Translate<TestEntity>(x => x.OptionalAge == null).Should().Be("OptionalAge eq null");

    // --- String escape ---

    [Fact]
    public void StringWithSingleQuote() =>
        Translate<TestEntity>(x => x.Name == "O'Brien").Should().Be("Name eq 'O''Brien'");

    // --- Complex compound ---

    [Fact]
    public void ComplexCompound() =>
        Translate<TestEntity>(x => (x.Age > 18 && x.Age < 65) || x.Name == "Admin")
            .Should().Be("((Age gt 18 and Age lt 65) or Name eq 'Admin')");
}
