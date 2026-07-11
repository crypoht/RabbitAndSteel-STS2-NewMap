using MegaCrit.Sts2.Core.Map;
using STS2RitsuLib;

namespace RabbitAndSteelNewMap.Scripts.Map;

internal static class CustomMapNodeRegistry
{
    private const string ShopIconPath = "res://mod/Iamge/map_nodes/shop_like.png";
    private const string ShopOutlinePath = "res://mod/Iamge/map_nodes/shop_like_outline.png";
    private const string TreasureIconPath = "res://mod/Iamge/map_nodes/treasure_like.png";
    private const string TreasureOutlinePath = "res://mod/Iamge/map_nodes/treasure_like_outline.png";

    private static readonly Dictionary<int, Dictionary<MapCoord, CustomMapNodeKind>> NodesByAct = [];

    public static void Initialize()
    {
        RitsuLibFramework.SubscribeLifecycle<MapGeneratedEvent>(OnMapGenerated, replayCurrentState: false);
    }

    public static bool TryGetKind(int actIndex, MapCoord coord, out CustomMapNodeKind kind)
    {
        kind = CustomMapNodeKind.None;
        return NodesByAct.TryGetValue(actIndex, out var nodes)
               && nodes.TryGetValue(coord, out kind)
               && kind != CustomMapNodeKind.None;
    }

    public static bool TryGetIconPaths(CustomMapNodeKind kind, out string iconPath, out string outlinePath)
    {
        switch (kind)
        {
            case CustomMapNodeKind.ShopLike:
                iconPath = ShopIconPath;
                outlinePath = ShopOutlinePath;
                return true;
            case CustomMapNodeKind.TreasureLike:
                iconPath = TreasureIconPath;
                outlinePath = TreasureOutlinePath;
                return true;
            default:
                iconPath = string.Empty;
                outlinePath = string.Empty;
                return false;
        }
    }

    private static void OnMapGenerated(MapGeneratedEvent evt)
    {
        var candidates = evt.Map.GetAllMapPoints()
            .Where(point => point.CanBeModified && point.PointType is not MapPointType.Boss and not MapPointType.Ancient)
            .OrderBy(point => point.coord.row)
            .ThenBy(point => point.coord.col)
            .ToList();

        if (candidates.Count == 0)
            return;

        var rng = new Random(unchecked((int)(evt.RunState.Rng.Seed + (uint)((evt.ActIndex + 1) * 7919))));
        var selected = candidates[rng.Next(candidates.Count)];
        var kind = rng.Next(2) == 0 ? CustomMapNodeKind.ShopLike : CustomMapNodeKind.TreasureLike;

        selected.PointType = kind == CustomMapNodeKind.ShopLike
            ? MapPointType.Shop
            : MapPointType.Treasure;

        NodesByAct[evt.ActIndex] = new Dictionary<MapCoord, CustomMapNodeKind>
        {
            [selected.coord] = kind,
        };

        Entry.Logger.Info($"[CustomMapNode] Act {evt.ActIndex + 1}: placed {kind} shell at {selected.coord}.");
    }
}
