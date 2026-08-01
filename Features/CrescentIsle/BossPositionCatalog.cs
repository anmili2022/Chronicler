using System.Numerics;

namespace Chronicler;

internal sealed record BossPositionEntry(ExpeditionMap Map, int BossId, uint TerritoryType, Vector3 Position);

internal static class BossPositionCatalog
{
    private const uint NorthTerritoryType = 1346;

    private static readonly IReadOnlyList<BossPositionEntry> Positions =
    [
        CreateNorth(101, 500f, 0f, 56f),
        CreateNorth(102, 224f, 0f, 52f),
        CreateNorth(103, 659f, 0f, 132f),
        CreateNorth(104, 765f, 0f, 70f),
        CreateNorth(105, -150f, 0f, 70f),
        CreateNorth(106, -519f, 0f, 48f),
        CreateNorth(107, 152f, 0f, 70f),
        CreateNorth(108, -390f, 0f, 68f),
        CreateNorth(109, -82f, 0f, 12f),
        CreateNorth(110, 238f, 0f, 15f),
        CreateNorth(111, 807f, 0f, 61f),
        CreateNorth(112, -215f, 0f, 18f),
        CreateNorth(114, -688f, 0f, 90f),
        CreateNorth(116, -505.3f, 53.1f, 244f),
        CreateNorth(117, 233f, 7.7f, -470f),
        CreateNorth(2074, 724f, 70f, 220f),
        CreateNorth(2083, -661f, 87f, -54f),
        CreateNorth(2076, 95f, 10f, 470f),
        CreateNorth(2082, -855.7f, 70.7f, 482.2f),
        CreateNorth(2080, -90f, 67.5f, 866f),
        CreateNorth(2075, 510f, 16.8f, -30f),
        CreateNorth(2081, -440f, 47f, -790f),
        CreateNorth(2084, 140f, 37f, -708f),
        CreateNorth(2077, 330f, 0f, -250f),
        CreateNorth(2079, -170f, 30f, -500f),
        CreateNorth(2078, -402f, 29.8f, -253f),
    ];

    public static BossPositionEntry? Find(BossEntry boss)
        => Positions.FirstOrDefault(entry => entry.Map == boss.Map && entry.BossId == boss.Id);

    private static BossPositionEntry CreateNorth(int bossId, float x, float y, float z)
        => new(ExpeditionMap.North, bossId, NorthTerritoryType, new Vector3(x, y, z));
}
