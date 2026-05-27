using System.Linq.Expressions;
using OLinq.Exceptions;

namespace OLinq.Translation;

internal static class ODataOrderByVisitor
{
    public static string Translate(LambdaExpression keySelector, bool descending)
    {
        // Strip outer Convert/TypeAs (lambda returns object but property is string, etc.)
        var body = StripConvert(keySelector.Body);
        var path = GetPropertyPath(body);
        return descending ? $"{path} desc" : $"{path} asc";
    }

    private static Expression StripConvert(Expression expr)
    {
        while (expr is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } u)
            expr = u.Operand;
        return expr;
    }

    private static string GetPropertyPath(Expression expression)
    {
        var parts = new List<string>();
        var current = expression;

        while (current is MemberExpression member)
        {
            parts.Add(ODataMemberNameResolver.Resolve(member.Member));
            current = member.Expression;
        }

        if (parts.Count == 0)
            throw new ODataTranslationException($"Cannot resolve property path from: {expression}");

        parts.Reverse();
        return string.Join("/", parts);
    }
}
