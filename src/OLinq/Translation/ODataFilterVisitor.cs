using System.Collections;
using System.Linq.Expressions;
using OLinq.Exceptions;

namespace OLinq.Translation;

internal sealed class ODataFilterVisitor : ExpressionVisitor
{
    private readonly System.Text.StringBuilder _sb = new();
    private readonly Dictionary<ParameterExpression, string> _parameterAliases;

    private ODataFilterVisitor(Dictionary<ParameterExpression, string>? aliases = null)
    {
        _parameterAliases = aliases ?? [];
    }

    public static string Translate(Expression expression)
    {
        var visitor = new ODataFilterVisitor();
        visitor.Visit(expression);
        return visitor._sb.ToString();
    }

    private string TranslateInner(Expression expression, Dictionary<ParameterExpression, string> aliases)
    {
        var inner = new ODataFilterVisitor(aliases);
        inner.Visit(expression);
        return inner._sb.ToString();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var op = node.NodeType switch
        {
            ExpressionType.Equal => "eq",
            ExpressionType.NotEqual => "ne",
            ExpressionType.GreaterThan => "gt",
            ExpressionType.GreaterThanOrEqual => "ge",
            ExpressionType.LessThan => "lt",
            ExpressionType.LessThanOrEqual => "le",
            ExpressionType.AndAlso => "and",
            ExpressionType.OrElse => "or",
            ExpressionType.Add => "add",
            ExpressionType.Subtract => "sub",
            ExpressionType.Multiply => "mul",
            ExpressionType.Divide => "div",
            ExpressionType.Modulo => "mod",
            _ => throw new ODataTranslationException($"Unsupported binary operator: {node.NodeType}")
        };

        var isLogical = node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse;

        if (isLogical)
        {
            _sb.Append('(');
            Visit(node.Left);
            _sb.Append($" {op} ");
            Visit(node.Right);
            _sb.Append(')');
        }
        else if (IsNullLiteral(node.Right))
        {
            Visit(node.Left);
            _sb.Append($" {op} null");
        }
        else if (IsNullLiteral(node.Left))
        {
            _sb.Append($"null {op} ");
            Visit(node.Right);
        }
        else
        {
            Visit(node.Left);
            _sb.Append($" {op} ");
            Visit(node.Right);
        }

        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        switch (node.NodeType)
        {
            case ExpressionType.Not:
                _sb.Append("not (");
                Visit(node.Operand);
                _sb.Append(')');
                break;
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
            case ExpressionType.TypeAs:
                Visit(node.Operand);
                break;
            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                _sb.Append('-');
                Visit(node.Operand);
                break;
            default:
                throw new ODataTranslationException($"Unsupported unary operator: {node.NodeType}");
        }
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // DateTime/DateTimeOffset property access as OData functions
        if (node.Expression is not null)
        {
            var underlyingType = Nullable.GetUnderlyingType(node.Expression.Type) ?? node.Expression.Type;

            if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
            {
                var func = node.Member.Name switch
                {
                    "Year" => "year",
                    "Month" => "month",
                    "Day" => "day",
                    "Hour" => "hour",
                    "Minute" => "minute",
                    "Second" => "second",
                    "Date" => "date",
                    "TimeOfDay" => "time",
                    _ => null
                };

                if (func is not null)
                {
                    _sb.Append($"{func}(");
                    Visit(node.Expression);
                    _sb.Append(')');
                    return node;
                }
            }

            // string.Length
            if (underlyingType == typeof(string) && node.Member.Name == "Length")
            {
                _sb.Append("length(");
                Visit(node.Expression);
                _sb.Append(')');
                return node;
            }

            // Nullable<T>.HasValue
            if (node.Member.Name == "HasValue" && Nullable.GetUnderlyingType(node.Expression.Type) is not null)
            {
                Visit(node.Expression);
                _sb.Append(" ne null");
                return node;
            }

            // Nullable<T>.Value - just unwrap
            if (node.Member.Name == "Value" && Nullable.GetUnderlyingType(node.Expression.Type) is not null)
            {
                Visit(node.Expression);
                return node;
            }
        }

        // Captured closure variable - evaluate it
        if (IsClosureAccess(node))
        {
            var value = Evaluate(node);
            _sb.Append(ODataValueFormatter.Format(value));
            return node;
        }

        // Regular property path (handles parameter aliases too)
        _sb.Append(ResolveMemberPath(node));
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameterAliases.TryGetValue(node, out var alias))
            _sb.Append(alias);
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        // Skip the root queryable constant
        if (node.Value is IQueryable)
            return node;
        _sb.Append(ODataValueFormatter.Format(node.Value));
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;
        var declType = method.DeclaringType;

        // string instance methods
        if (declType == typeof(string))
        {
            if (TryHandleStringMethod(node, method.Name))
                return node;
        }

        // Math functions
        if (declType == typeof(Math))
        {
            if (TryHandleMathMethod(node, method.Name))
                return node;
        }

        // Enumerable.Contains static: Enumerable.Contains(collection, item)
        if (method.IsStatic && (declType == typeof(Enumerable) || declType == typeof(Queryable))
            && method.Name == "Contains" && node.Arguments.Count == 2)
        {
            if (TryGetCollection(node.Arguments[0], out var items))
            {
                Visit(node.Arguments[1]);
                AppendInList(items!);
                return node;
            }
        }

        // Instance Contains on collections: myList.Contains(x.Prop)
        if (!method.IsStatic && method.Name == "Contains" && node.Object is not null && node.Arguments.Count == 1)
        {
            if (TryGetCollection(node.Object, out var items))
            {
                Visit(node.Arguments[0]);
                AppendInList(items!);
                return node;
            }
        }

        // Any/All on collection navigation properties
        if (method.Name is "Any" or "All")
        {
            LambdaExpression? lambda = null;
            Expression source;

            if (method.IsStatic)
            {
                source = node.Arguments[0];
                if (node.Arguments.Count == 2)
                    lambda = ExtractLambdaFromArg(node.Arguments[1]);
            }
            else
            {
                source = node.Object!;
                if (node.Arguments.Count == 1)
                    lambda = ExtractLambdaFromArg(node.Arguments[0]);
            }

            var collPath = ResolveMemberPath(source);
            var odataFunc = method.Name == "Any" ? "any" : "all";

            if (lambda is not null)
            {
                var param = lambda.Parameters[0];
                var aliasName = param.Name ?? "x";
                var newAliases = new Dictionary<ParameterExpression, string>(_parameterAliases)
                {
                    [param] = aliasName
                };
                var innerText = TranslateInner(lambda.Body, newAliases);
                _sb.Append($"{collPath}/{odataFunc}({aliasName}: {innerText})");
            }
            else
            {
                _sb.Append($"{collPath}/{odataFunc}()");
            }
            return node;
        }

        // Fallback: try to evaluate as a local constant expression
        try
        {
            var value = Evaluate(node);
            _sb.Append(ODataValueFormatter.Format(value));
            return node;
        }
        catch
        {
            throw new ODataTranslationException(
                $"Cannot translate method call: {declType?.Name}.{method.Name}. " +
                "If this is a local computation, ensure it can be evaluated at query build time.");
        }
    }

    // --- String method handling ---
    private bool TryHandleStringMethod(MethodCallExpression node, string name)
    {
        switch (name)
        {
            case "Contains" when node.Arguments.Count == 1:
                _sb.Append("contains("); Visit(node.Object!); _sb.Append(", "); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "StartsWith" when node.Arguments.Count == 1:
                _sb.Append("startswith("); Visit(node.Object!); _sb.Append(", "); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "EndsWith" when node.Arguments.Count == 1:
                _sb.Append("endswith("); Visit(node.Object!); _sb.Append(", "); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "ToLower" or "ToLowerInvariant":
                _sb.Append("tolower("); Visit(node.Object!); _sb.Append(')');
                return true;
            case "ToUpper" or "ToUpperInvariant":
                _sb.Append("toupper("); Visit(node.Object!); _sb.Append(')');
                return true;
            case "Trim":
                _sb.Append("trim("); Visit(node.Object!); _sb.Append(')');
                return true;
            case "IndexOf" when node.Arguments.Count == 1:
                _sb.Append("indexof("); Visit(node.Object!); _sb.Append(", "); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "Substring" when node.Arguments.Count == 1:
                _sb.Append("substring("); Visit(node.Object!); _sb.Append(", "); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "Substring" when node.Arguments.Count == 2:
                _sb.Append("substring("); Visit(node.Object!); _sb.Append(", "); Visit(node.Arguments[0]); _sb.Append(", "); Visit(node.Arguments[1]); _sb.Append(')');
                return true;
            case "Concat" when node.Arguments.Count == 2:
                _sb.Append("concat("); Visit(node.Arguments[0]); _sb.Append(", "); Visit(node.Arguments[1]); _sb.Append(')');
                return true;
        }
        return false;
    }

    // --- Math method handling ---
    private bool TryHandleMathMethod(MethodCallExpression node, string name)
    {
        switch (name)
        {
            case "Round" when node.Arguments.Count == 1:
                _sb.Append("round("); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "Floor":
                _sb.Append("floor("); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "Ceiling":
                _sb.Append("ceiling("); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
            case "Abs":
                _sb.Append("abs("); Visit(node.Arguments[0]); _sb.Append(')');
                return true;
        }
        return false;
    }

    private void AppendInList(IEnumerable items)
    {
        _sb.Append(" in (");
        _sb.Append(string.Join(", ", items.Cast<object?>().Select(ODataValueFormatter.Format)));
        _sb.Append(')');
    }

    private string ResolveMemberPath(Expression expression)
    {
        var parts = new List<string>();
        var current = expression;

        while (current is MemberExpression member)
        {
            parts.Add(ODataMemberNameResolver.Resolve(member.Member));
            current = member.Expression;
        }

        if (current is ParameterExpression param && _parameterAliases.TryGetValue(param, out var alias))
        {
            parts.Reverse();
            return parts.Count > 0 ? $"{alias}/{string.Join("/", parts)}" : alias;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static LambdaExpression ExtractLambdaFromArg(Expression arg) =>
        arg is UnaryExpression { NodeType: ExpressionType.Quote } quote
            ? (LambdaExpression)quote.Operand
            : (LambdaExpression)arg;

    private static bool IsNullLiteral(Expression expr) =>
        expr is ConstantExpression { Value: null } ||
        (expr is UnaryExpression { NodeType: ExpressionType.Convert } u && IsNullLiteral(u.Operand));

    private static bool IsClosureAccess(MemberExpression node)
    {
        var current = (Expression)node;
        while (current is MemberExpression m)
            current = m.Expression!;
        return current is ConstantExpression;
    }

    private static object? Evaluate(Expression expr) =>
        Expression.Lambda(expr).Compile().DynamicInvoke();

    private static bool TryGetCollection(Expression expr, out IEnumerable? items)
    {
        try
        {
            var v = Evaluate(expr);
            if (v is IEnumerable e and not string)
            {
                items = e;
                return true;
            }
        }
        catch { }
        items = null;
        return false;
    }
}
