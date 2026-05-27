using System.Linq.Expressions;
using OLinq.Exceptions;

namespace OLinq.Translation;

internal static class ODataSelectVisitor
{
    public static string Translate(LambdaExpression selector)
    {
        // Strip outer Convert/TypeAs casts (e.g. x => (object)x.Name)
        var body = StripConvert(selector.Body);

        return body switch
        {
            NewExpression newExpr => TranslateNew(newExpr),
            MemberInitExpression init => TranslateMemberInit(init),
            MemberExpression member => GetMemberPath(member),
            _ => throw new ODataTranslationException($"Unsupported select expression type: {body.GetType().Name}")
        };
    }

    private static Expression StripConvert(Expression expr)
    {
        while (expr is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } u)
            expr = u.Operand;
        return expr;
    }

    private static string TranslateNew(NewExpression newExpr)
    {
        if (newExpr.Members is null || newExpr.Members.Count == 0)
            throw new ODataTranslationException("Select expression must have at least one member.");

        var names = new List<string>();
        foreach (var member in newExpr.Members)
            names.Add(ODataMemberNameResolver.Resolve(member));

        return string.Join(",", names);
    }

    private static string TranslateMemberInit(MemberInitExpression init)
    {
        var names = new List<string>();
        foreach (var binding in init.Bindings)
            names.Add(ODataMemberNameResolver.Resolve(binding.Member));
        return string.Join(",", names);
    }

    private static string GetMemberPath(MemberExpression node)
    {
        var parts = new List<string>();
        Expression? current = node;
        while (current is MemberExpression m)
        {
            parts.Add(ODataMemberNameResolver.Resolve(m.Member));
            current = m.Expression;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
