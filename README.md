# OLinq

A .NET library that translates LINQ expressions into OData query strings, letting you build type-safe OData queries with the syntax you already know.

```csharp
var products = await httpClient
    .ODataQuery<Product>("https://api.example.com/odata/Products")
    .Where(p => p.Category == "Electronics" && p.Price < 500)
    .OrderByDescending(p => p.Rating)
    .Take(10)
    .WithCount()
    .ToCollectionAsync();

Console.WriteLine($"Showing {products.Items.Count} of {products.TotalCount} results");
```

Produces: `?$filter=Category%20eq%20'Electronics'%20and%20Price%20lt%20500&$orderby=Rating%20desc&$top=10&$count=true`

---

## Installation

```bash
dotnet add package OLinq
```

Requires .NET 9.0 or later.

---

## Quick Start

Register an `HttpClient` and create a queryable targeting your entity set URL:

```csharp
using OLinq.Extensions;

// From any HttpClient
var query = httpClient.ODataQuery<Product>("https://api.example.com/odata/Products");

// Chain LINQ operators
var results = await query
    .Where(p => p.IsAvailable)
    .Select(p => new { p.Name, p.Price })
    .OrderBy(p => p.Name)
    .Skip(20)
    .Take(10)
    .ToListAsync();
```

With `Microsoft.Extensions.Http` and DI:

```csharp
// Program.cs
builder.Services.AddHttpClient<ProductService>(c =>
    c.BaseAddress = new Uri("https://api.example.com/odata/"));

// ProductService.cs
public class ProductService(HttpClient httpClient)
{
    private IQueryable<Product> Query =>
        httpClient.ODataQuery<Product>("Products");

    public Task<IReadOnlyList<Product>> GetCheapElectronicsAsync() =>
        Query.Where(p => p.Category == "Electronics" && p.Price < 100)
             .OrderBy(p => p.Price)
             .ToListAsync();
}
```

---

## Supported LINQ Operations

### Filtering — `Where`

All filter predicates translate to `$filter`. Multiple `.Where()` calls are combined with `and`.

#### Comparison operators

```csharp
.Where(x => x.Name == "Alice")        // Name eq 'Alice'
.Where(x => x.Name != "Alice")        // Name ne 'Alice'
.Where(x => x.Age > 18)               // Age gt 18
.Where(x => x.Age >= 18)              // Age ge 18
.Where(x => x.Age < 65)               // Age lt 65
.Where(x => x.Age <= 65)              // Age le 65
.Where(x => x.Name == null)           // Name eq null
.Where(x => x.Name != null)           // Name ne null
```

#### Logical operators

```csharp
.Where(x => x.Age > 18 && x.IsActive)          // (Age gt 18) and (IsActive)
.Where(x => x.Name == "A" || x.Name == "B")    // (Name eq 'A') or (Name eq 'B')
.Where(x => !x.IsDeleted)                       // not (IsDeleted)
```

#### Arithmetic

```csharp
.Where(x => x.Price * 1.2 < 100)     // Price mul 1.2 lt 100
.Where(x => x.Quantity + 5 > 10)     // Quantity add 5 gt 10
.Where(x => x.Total - x.Tax > 0)     // Total sub Tax gt 0
.Where(x => x.Score / 2 >= 5)        // Score div 2 ge 5
.Where(x => x.Count % 3 == 0)        // Count mod 3 eq 0
```

#### String functions

```csharp
.Where(x => x.Name.Contains("Ali"))              // contains(Name, 'Ali')
.Where(x => x.Name.StartsWith("Al"))             // startswith(Name, 'Al')
.Where(x => x.Name.EndsWith("ce"))               // endswith(Name, 'ce')
.Where(x => x.Name.ToLower() == "alice")         // tolower(Name) eq 'alice'
.Where(x => x.Name.ToUpper() == "ALICE")         // toupper(Name) eq 'ALICE'
.Where(x => x.Name.Trim() == "Alice")            // trim(Name) eq 'Alice'
.Where(x => x.Name.Length > 3)                   // length(Name) gt 3
.Where(x => x.Name.IndexOf("l") == 1)            // indexof(Name, 'l') eq 1
.Where(x => x.Name.Substring(0, 3) == "Ali")     // substring(Name, 0, 3) eq 'Ali'
.Where(x => x.Name.Substring(1) == "lice")       // substring(Name, 1) eq 'lice'
.Where(x => string.Concat(x.First, x.Last) == "AliceDoe")  // concat(First, Last) eq 'AliceDoe'
```

#### Math functions

```csharp
.Where(x => Math.Round(x.Score) == 5)     // round(Score) eq 5
.Where(x => Math.Floor(x.Score) == 4)     // floor(Score) eq 4
.Where(x => Math.Ceiling(x.Score) == 5)   // ceiling(Score) eq 5
.Where(x => Math.Abs(x.Balance) > 0)      // abs(Balance) gt 0
```

#### Date and time functions

```csharp
.Where(x => x.CreatedAt.Year == 2024)     // year(CreatedAt) eq 2024
.Where(x => x.CreatedAt.Month == 12)      // month(CreatedAt) eq 12
.Where(x => x.CreatedAt.Day == 25)        // day(CreatedAt) eq 25
.Where(x => x.CreatedAt.Hour == 9)        // hour(CreatedAt) eq 9
.Where(x => x.CreatedAt.Minute == 30)     // minute(CreatedAt) eq 30
.Where(x => x.CreatedAt.Second == 0)      // second(CreatedAt) eq 0
.Where(x => x.CreatedAt.Date == someDate) // date(CreatedAt) eq 2024-12-25
```

#### Nullable properties

```csharp
.Where(x => x.OptionalAge.HasValue)       // OptionalAge ne null
.Where(x => x.OptionalAge == null)        // OptionalAge eq null
```

#### Nested (navigation) properties

```csharp
.Where(x => x.Address.City == "London")              // Address/City eq 'London'
.Where(x => x.Company.Address.Country == "UK")       // Company/Address/Country eq 'UK'
```

#### Collection `in` operator

```csharp
var ids = new[] { 1, 2, 3 };
.Where(x => ids.Contains(x.Id))           // Id in (1, 2, 3)

var names = new List<string> { "Alice", "Bob" };
.Where(x => names.Contains(x.Name))       // Name in ('Alice', 'Bob')
```

#### Collection lambdas — `any` / `all`

```csharp
// Products that have at least one tag equal to "sale"
.Where(x => x.Tags.Any(t => t == "sale"))
// Tags/any(t: t eq 'sale')

// Orders where all items are in stock
.Where(x => x.Items.All(i => i.InStock))
// Items/all(i: i/InStock)

// Orders with any high-value item
.Where(x => x.Items.Any(i => i.Price > 1000))
// Items/any(i: i/Price gt 1000)
```

#### Local variables and computed values

Local variables are evaluated at query-build time and embedded as literals:

```csharp
var minAge = GetMinimumAge();   // returns 18
var cutoff = DateTime.UtcNow.AddDays(-30);

.Where(x => x.Age >= minAge && x.CreatedAt >= cutoff)
// Age ge 18 and CreatedAt ge 2024-11-27T00:00:00Z
```

---

### Projection — `Select`

```csharp
// Anonymous type — produces $select=Name,Price
.Select(x => new { x.Name, x.Price })

// Named type
.Select(x => new ProductDto { Name = x.Name, Price = x.Price })

// Single property — produces $select=Name
.Select(x => x.Name)

// Nested property — produces $select=Address/City (if server supports)
.Select(x => x.Address.City)
```

---

### Sorting — `OrderBy` / `ThenBy`

```csharp
.OrderBy(x => x.Name)                          // $orderby=Name asc
.OrderByDescending(x => x.Price)               // $orderby=Price desc
.OrderBy(x => x.Category).ThenBy(x => x.Name) // $orderby=Category asc,Name asc
.OrderBy(x => x.Name).ThenByDescending(x => x.Age) // $orderby=Name asc,Age desc
.OrderBy(x => x.Address.City)                  // $orderby=Address/City asc
```

`OrderBy` resets the sort list; `ThenBy` appends to it.

---

### Paging — `Take` / `Skip`

```csharp
.Take(25)          // $top=25
.Skip(50)          // $skip=50
.Skip(50).Take(25) // $skip=50&$top=25
```

---

### Expand (navigation properties) — `Expand` / `ExpandNested`

```csharp
using OLinq.Extensions;

// Simple expand — $expand=Orders
.Expand(x => x.Orders)

// Multiple expands — $expand=Orders,Customer
.Expand(x => x.Orders)
.Expand(x => x.Customer)

// Nested expand with query options — $expand=Orders($select=Amount,Status;$top=5)
.ExpandNested(
    x => x.Orders,
    q => q.Select(o => new { o.Amount, o.Status }).Take(5))
```

---

### Full-text search — `Search`

```csharp
.Search("mountain bike")   // $search=mountain%20bike
```

---

### Include total count — `WithCount`

```csharp
.WithCount()   // $count=true
```

Use with `ToCollectionAsync()` to read `TotalCount`:

```csharp
var result = await query.WithCount().ToCollectionAsync();
Console.WriteLine($"Page 1 of {Math.Ceiling(result.TotalCount!.Value / 10.0)}");
```

---

### Aggregations — `Apply`

Pass a raw `$apply` expression for server-side aggregation:

```csharp
// Group by category and count
.Apply("groupby((Category),aggregate($count as Count))")

// Filter then aggregate
.Apply("filter(Price gt 100)/groupby((Category),aggregate(Price with sum as TotalRevenue))")
```

---

## Execution Methods

| Method | Returns | When to use |
|--------|---------|-------------|
| `ToListAsync()` | `IReadOnlyList<T>` | Just the items, no metadata |
| `ToCollectionAsync()` | `ODataCollection<T>` | Items + `TotalCount` + `NextLink` |
| `ToODataUrl()` | `string` | Inspect the generated URL without executing |

```csharp
// Items only
IReadOnlyList<Product> items = await query.ToListAsync();

// Items + pagination metadata
ODataCollection<Product> page = await query.WithCount().ToCollectionAsync();
Console.WriteLine(page.TotalCount);   // total matching records
Console.WriteLine(page.NextLink);     // link to the next page (if any)

// URL inspection (useful for debugging or logging)
string url = query.Where(x => x.Price > 100).ToODataUrl();
// https://api.example.com/odata/Products?$filter=Price%20gt%20100
```

---

## Custom Property Names

Use `[ODataPropertyName]` when your CLR property name differs from the OData property name:

```csharp
using OLinq.Attributes;

public class Product
{
    [ODataPropertyName("product_name")]
    public string Name { get; set; }

    [ODataPropertyName("unit_price")]
    public decimal Price { get; set; }
}

// .Where(x => x.Name == "Widget") → $filter=product_name eq 'Widget'
// .OrderBy(x => x.Price)          → $orderby=unit_price asc
```

---

## Supported Literal Types

| C# Type | OData Literal Example |
|---------|----------------------|
| `string` | `'Hello world'` (single quotes escaped as `''`) |
| `int` | `42` |
| `long` | `42L` |
| `float` | `3.14f` |
| `double` | `3.14` |
| `decimal` | `9.99M` |
| `bool` | `true` / `false` |
| `Guid` | `00000000-0000-0000-0000-000000000000` |
| `DateTime` (UTC) | `2024-12-25T09:00:00Z` |
| `DateTime` (local) | `2024-12-25T09:00:00` |
| `DateTimeOffset` | `2024-12-25T09:00:00+01:00` |
| `DateOnly` | `2024-12-25` |
| `TimeOnly` | `09:00:00.0000000` |
| `TimeSpan` | `duration'P1DT2H'` |
| `null` | `null` |
| `enum` | `'Active'` |
| `byte[]` | `binary'SGVsbG8='` |

---

## Error Handling

Unsupported expressions throw `ODataTranslationException` at query-build time (before any HTTP call):

```csharp
using OLinq.Exceptions;

try
{
    var url = query.Where(x => SomeComplexLocalMethod(x)).ToODataUrl();
}
catch (ODataTranslationException ex)
{
    // "Cannot translate method call: ..."
}
```

---

## Architecture

OLinq implements the standard .NET LINQ provider pattern:

| Component | Role |
|-----------|------|
| `ODataQueryable<T>` | `IOrderedQueryable<T>` — the fluent query object |
| `ODataQueryProvider` | `IQueryProvider` — drives expression translation and HTTP execution |
| `ODataExpressionTranslator` | Unwinds the LINQ method call chain into query options |
| `ODataFilterVisitor` | `ExpressionVisitor` that builds the `$filter` string |
| `ODataSelectVisitor` | Translates `Select` projections to `$select` |
| `ODataOrderByVisitor` | Translates `OrderBy`/`ThenBy` to `$orderby` |
| `ODataQueryContext` | Accumulates all query options and builds the final query string |
| `ODataValueFormatter` | Converts CLR constant values to OData literal syntax |
| `ODataMemberNameResolver` | Resolves property names (with `[ODataPropertyName]` support) |

---

## License

MIT
