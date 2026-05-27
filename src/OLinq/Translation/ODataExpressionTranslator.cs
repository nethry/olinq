using System.Linq.Expressions;
using OLinq.Exceptions;

namespace OLinq.Translation;

internal sealed class ODataExpressionTranslator : ExpressionVisitor
{
    private readonly ODataQueryContext _context = new();

    public static ODataQueryContext Translate(Expression expression)
    {
        var translator = new ODataExpressionTranslator();
        translator.Visit(expression);
        return translator._context;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // First recurse into the source (unwinding the call chain from outermost to innermost)
        Visit(node.Arguments[0]);

        switch (node.Method.Name)
        {
            case "Where":
            {
                var lambda = ExtractLambda(node.Arguments[1]);
                var filter = ODataFilterVisitor.Translate(lambda.Body);
                _context.Filter = _context.Filter is not null
                    ? $"({_context.Filter}) and ({filter})"
                    : filter;
                break;
            }

            case "Select":
            {
                var lambda = ExtractLambda(node.Arguments[1]);
                _context.Select = ODataSelectVisitor.Translate(lambda);
                break;
            }

            case "OrderBy":
            {
                var lambda = ExtractLambda(node.Arguments[1]);
                _context.OrderByParts.Clear();
                _context.OrderByParts.Add(ODataOrderByVisitor.Translate(lambda, descending: false));
                break;
            }

            case "OrderByDescending":
            {
                var lambda = ExtractLambda(node.Arguments[1]);
                _context.OrderByParts.Clear();
                _context.OrderByParts.Add(ODataOrderByVisitor.Translate(lambda, descending: true));
                break;
            }

            case "ThenBy":
            {
                var lambda = ExtractLambda(node.Arguments[1]);
                _context.OrderByParts.Add(ODataOrderByVisitor.Translate(lambda, descending: false));
                break;
            }

            case "ThenByDescending":
            {
                var lambda = ExtractLambda(node.Arguments[1]);
                _context.OrderByParts.Add(ODataOrderByVisitor.Translate(lambda, descending: true));
                break;
            }

            case "Take":
            {
                _context.Top = (int)ExtractConstant(node.Arguments[1]);
                break;
            }

            case "Skip":
            {
                _context.Skip = (int)ExtractConstant(node.Arguments[1]);
                break;
            }

            case "Expand":
            {
                if (node.Arguments.Count >= 2)
                {
                    var lambda = ExtractLambda(node.Arguments[1]);
                    var expandPath = GetMemberPath(lambda.Body);
                    _context.ExpandParts.Add(expandPath);
                }
                break;
            }

            case "Search":
            {
                _context.Search = (string)ExtractConstant(node.Arguments[1]);
                break;
            }

            case "WithCount":
            {
                _context.IncludeCount = true;
                break;
            }

            case "Apply":
            {
                _context.Apply = (string)ExtractConstant(node.Arguments[1]);
                break;
            }

            case "ExpandNested":
            {
                if (node.Arguments.Count >= 3)
                {
                    var navLambda = ExtractLambda(node.Arguments[1]);
                    var navPath = GetMemberPath(navLambda.Body);
                    var nestedLambda = ExtractLambda(node.Arguments[2]);

                    // nestedLambda is: q => q.Select(...).Take(5) etc.
                    // We translate the body which is a call chain starting from the parameter
                    var nestedContext = TranslateNestedQuery(nestedLambda);
                    var nestedQuery = BuildNestedQueryOptions(nestedContext);

                    _context.ExpandParts.Add(nestedQuery.Length > 0
                        ? $"{navPath}({nestedQuery})"
                        : navPath);
                }
                break;
            }
        }

        return node;
    }

    private static ODataQueryContext TranslateNestedQuery(LambdaExpression nestedLambda)
    {
        // The nested lambda body is a chain of method calls on the parameter
        // We need to translate it into query context options
        var nestedTranslator = new ODataExpressionTranslator();

        // Replace the parameter with a constant so our visitor can traverse
        var paramReplacer = new ParameterReplacingVisitor(
            nestedLambda.Parameters[0],
            Expression.Constant(null, nestedLambda.Parameters[0].Type));

        var body = paramReplacer.Visit(nestedLambda.Body);
        nestedTranslator.Visit(body);
        return nestedTranslator._context;
    }

    private static string BuildNestedQueryOptions(ODataQueryContext ctx)
    {
        var parts = new List<string>();

        if (ctx.Filter is not null) parts.Add($"$filter={Uri.EscapeDataString(ctx.Filter)}");
        if (ctx.Select is not null) parts.Add($"$select={ctx.Select}");
        if (ctx.OrderByParts.Count > 0) parts.Add($"$orderby={Uri.EscapeDataString(string.Join(",", ctx.OrderByParts))}");
        if (ctx.Top.HasValue) parts.Add($"$top={ctx.Top.Value}");
        if (ctx.Skip.HasValue) parts.Add($"$skip={ctx.Skip.Value}");

        return string.Join(";", parts);
    }

    private static LambdaExpression ExtractLambda(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Quote } unary
            ? (LambdaExpression)unary.Operand
            : (LambdaExpression)expression;

    private static object ExtractConstant(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value!;
        return Expression.Lambda(expression).Compile().DynamicInvoke()!;
    }

    private static string GetMemberPath(Expression body)
    {
        // Strip Convert wrapping
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } u)
            body = u.Operand;

        var parts = new List<string>();
        var current = body;
        while (current is MemberExpression member)
        {
            parts.Add(ODataMemberNameResolver.Resolve(member.Member));
            current = member.Expression;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>Replaces a parameter expression with a substitute so the method-call chain can be traversed.</summary>
    private sealed class ParameterReplacingVisitor(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
