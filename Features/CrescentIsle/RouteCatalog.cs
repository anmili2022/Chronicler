namespace Chronicler;

/// <summary>内置预设路线（随版本分发，所有用户自带）+ 与用户自定义路线的合并解析。</summary>
internal static class RouteCatalog
{
    /// <summary>内置预设路线，由 /shiguan route export 生成后粘贴进 BuildRoutes。</summary>
    public static readonly IReadOnlyList<BossRouteDto> BuiltInRoutes = BuildRoutes();

    /// <summary>合并解析指定 Boss 的可用路线：用户自定义优先，其次内置。</summary>
    public static IReadOnlyList<BossRouteDto> GetRoutes(ExpeditionMap map, int bossId, PluginConfiguration config)
    {
        var routes = new List<BossRouteDto>();
        foreach (var route in BuiltInRoutes)
        {
            if (route.Map == map && route.BossId == bossId && route.Points.Count >= 2)
                routes.Add(route);
        }

        foreach (var route in config.BossRoutes)
        {
            if (route.Map != map || route.BossId != bossId || route.Points.Count < 2)
                continue;

            var overridden = routes.FindIndex(existing => existing.RouteIndex == route.RouteIndex);
            if (overridden >= 0)
                routes[overridden] = route;
            else
                routes.Add(route);
        }

        return routes;
    }

    private static BossRouteDto Route(ExpeditionMap map, int bossId, int routeIndex, params (float X, float Y, float Z)[] points)
    {
        return new BossRouteDto
        {
            Map = map,
            BossId = bossId,
            RouteIndex = routeIndex,
            Points = points.Select(point => new BossRoutePointDto(point.X, point.Y, point.Z)).ToList(),
        };
    }

    private static BossRouteDto Route(ExpeditionMap map, int bossId, int routeIndex, params BossRoutePointDto[] points)
    {
        return new BossRouteDto
        {
            Map = map,
            BossId = bossId,
            RouteIndex = routeIndex,
            Points = points.ToList(),
        };
    }

    private static BossRoutePointDto Point(float x, float y, float z, BossRoutePointKind kind = BossRoutePointKind.Normal)
        => new(x, y, z, kind);

    private static List<BossRouteDto> BuildRoutes()
    {
        return
        [
            Route(ExpeditionMap.North, 116, 0,
                Point(-557.75f, 66.92f, 585.18f),
                Point(-542.59f, 57.78f, 511.08f, BossRoutePointKind.Forced),
                Point(-484.57f, 36.38f, 411.59f, BossRoutePointKind.Forced),
                Point(-511.51f, 41.01f, 379.66f),
                Point(-520.81f, 53.13f, 327.14f),
                Point(-504.92f, 53.16f, 246.14f, BossRoutePointKind.Forced)
            ),
            Route(ExpeditionMap.North, 116, 1,
                Point(-557.75f, 66.92f, 585.18f),
                Point(-552.19f, 65.49f, 551.53f),
                Point(-543.96f, 53.85f, 495.71f, BossRoutePointKind.Forced),
                Point(-586.85f, 50.99f, 424.56f, BossRoutePointKind.Forced),
                Point(-626.54f, 60.03f, 369.73f),
                Point(-567.43f, 53.79f, 325.21f),
                Point(-505.26f, 53.14f, 243.91f, BossRoutePointKind.Forced)
            ),
            Route(ExpeditionMap.North, 116, 2,
                Point(-557.75f, 66.92f, 585.18f),
                Point(-505.26f, 53.14f, 243.91f)
            ),
        ];
    }
}
