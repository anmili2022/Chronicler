using System.Numerics;

namespace Chronicler;

internal sealed record BossPositionEntry(ExpeditionMap Map, int BossId, uint TerritoryType, Vector3 Position);

internal static class BossPositionCatalog
{
    private const uint NorthTerritoryType = 1346;
    private const uint SouthTerritoryType = 1252;

    private static readonly IReadOnlyList<BossPositionEntry> Positions =
    [
        CreateSouth(1962, 162f, 56f, 676f),
        CreateSouth(1963, 373.2f, 70f, 486f),
        CreateSouth(1964, -226.1f, 116.4f, 254f),
        CreateSouth(1965, -548.5f, 3f, -595f),
        CreateSouth(1966, -223.1f, 107f, 36f),
        CreateSouth(1967, -48.1f, 111.8f, -320f),
        CreateSouth(1968, -370f, 75f, 650f),
        CreateSouth(1969, -589.1f, 96.5f, 333f),
        CreateSouth(1970, -71f, 71.3f, 557f),
        CreateSouth(1971, 79f, 97.9f, 278f),
        CreateSouth(1972, 413f, 96f, -13f),
        CreateSouth(16, 200f, 111.7f, -215f),
        CreateSouth(17, -481f, 75f, 528f),
        CreateNorth(101, 500f, 56f, -310f),
        CreateNorth(102, 224f, 52f, -860f),
        CreateNorth(103, 659f, 132f, 659f),
        CreateNorth(104, 765f, 70f, 0f),
        CreateNorth(105, -150f, 70f, -860f),
        CreateNorth(106, -519f, 48f, -641f),
        CreateNorth(107, 152f, 70f, 716f),
        CreateNorth(108, -390f, 68f, 700f),
        CreateNorth(109, -82f, 12f, 485f),
        CreateNorth(110, 238f, 15f, 367f),
        CreateNorth(111, 807f, 61f, -562f),
        CreateNorth(112, -215f, 18f, -65f),
        CreateNorth(113, -870f, 20f, -560f),
        CreateNorth(114, -688f, 90f, 150f),
        CreateNorth(115, 170f, 4f, -136f),
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

    private static BossPositionEntry CreateSouth(int bossId, float x, float y, float z)
        => new(ExpeditionMap.South, bossId, SouthTerritoryType, new Vector3(x, y, z));
}
