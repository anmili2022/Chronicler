using System.Numerics;

namespace Chronicler;

[Serializable]
public sealed class BossRoutePointDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public BossRoutePointKind Kind { get; set; } = BossRoutePointKind.Normal;

    public BossRoutePointDto() { }

    public BossRoutePointDto(float x, float y, float z, BossRoutePointKind kind = BossRoutePointKind.Normal)
    {
        X = x;
        Y = y;
        Z = z;
        Kind = kind;
    }

    public Vector3 ToVector3() => new(X, Y, Z);

    public static BossRoutePointDto FromVector3(Vector3 pos, BossRoutePointKind kind = BossRoutePointKind.Normal) => new(pos.X, pos.Y, pos.Z, kind);
}

public enum BossRoutePointKind
{
    Normal,
    Forced,
}

[Serializable]
public sealed class BossRouteDto
{
    public ExpeditionMap Map { get; set; }
    public int BossId { get; set; }
    public int RouteIndex { get; set; }
    public List<BossRoutePointDto> Points { get; set; } = new();
}
