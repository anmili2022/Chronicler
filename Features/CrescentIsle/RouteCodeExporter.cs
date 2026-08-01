using System.Text;

namespace Chronicler;

/// <summary>把自定义路线序列化为可粘贴进 RouteCatalog.BuildRoutes 的 C# 代码。</summary>
internal static class RouteCodeExporter
{
    public static string Export(IEnumerable<BossRouteDto> routes)
    {
        var list = routes
            .Where(route => route.Points.Count >= 2)
            .OrderBy(route => route.Map)
            .ThenBy(route => route.BossId)
            .ThenBy(route => route.RouteIndex)
            .ToList();

        if (list.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("        return");
        sb.AppendLine("        [");
        foreach (var route in list)
        {
            sb.AppendLine($"            Route(ExpeditionMap.{route.Map}, {route.BossId}, {route.RouteIndex},");
            for (var i = 0; i < route.Points.Count; i++)
            {
                var p = route.Points[i];
                var suffix = i == route.Points.Count - 1 ? "" : ",";
                var kind = p.Kind == BossRoutePointKind.Forced ? ", BossRoutePointKind.Forced" : string.Empty;
                sb.AppendLine($"                Point({p.X:F2}f, {p.Y:F2}f, {p.Z:F2}f{kind}){suffix}");
            }

            sb.AppendLine("            ),");
        }

        sb.AppendLine("        ];");
        return sb.ToString();
    }
}
