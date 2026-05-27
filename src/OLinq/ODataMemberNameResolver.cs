using System.Reflection;
using OLinq.Attributes;

namespace OLinq;

internal static class ODataMemberNameResolver
{
    private static readonly Dictionary<MemberInfo, string> Cache = new();
    private static readonly object Lock = new();

    public static string Resolve(MemberInfo member)
    {
        lock (Lock)
        {
            if (Cache.TryGetValue(member, out var cached))
                return cached;

            var attr = member.GetCustomAttribute<ODataPropertyNameAttribute>();
            var name = attr?.Name ?? member.Name;
            Cache[member] = name;
            return name;
        }
    }
}
