using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;
using STS2RitsuLib;

namespace RabbitAndSteelNewMap.Scripts.Map;

internal static class CustomMapNodeRegistry
{
    private const string ShopIconPath = "res://mod/Iamge/map_nodes/shop_like.png";
    private const string ShopOutlinePath = "res://mod/Iamge/map_nodes/shop_like_outline.png";
    private const string TreasureIconPath = "res://mod/Iamge/map_nodes/treasure_like.png";
    private const string TreasureOutlinePath = "res://mod/Iamge/map_nodes/treasure_like_outline.png";

    private static readonly ConditionalWeakTable<IRunState, Dictionary<int, Dictionary<MapCoord, CustomMapNodeKind>>> NodesByRun = new();

    public static void Initialize()
    {
        RitsuLibFramework.SubscribeLifecycle<MapGeneratedEvent>(OnMapGenerated, replayCurrentState: false);
    }

    public static bool TryGetKind(IRunState runState, int actIndex, MapCoord coord, out CustomMapNodeKind kind)
    {
        kind = CustomMapNodeKind.None;
        return NodesByRun.TryGetValue(runState, out var acts)
               && acts.TryGetValue(actIndex, out var nodes)
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
        var nodesByAct = NodesByRun.GetOrCreateValue(evt.RunState);
        nodesByAct.Remove(evt.ActIndex);
        var candidates = evt.Map.GetAllMapPoints()
            // Existing treasure and shop nodes always keep their vanilla identity.
            .Where(point => point.CanBeModified && point.PointType is not
                (MapPointType.Boss or MapPointType.Ancient or MapPointType.Treasure or MapPointType.Shop))
            .OrderBy(point => point.coord.row)
            .ThenBy(point => point.coord.col)
            .ToList();

        if (candidates.Count == 0)
            return;

        var rng = new Random(unchecked((int)(evt.RunState.Rng.Seed + (uint)((evt.ActIndex + 1) * 7919))));
        var selected = candidates[rng.Next(candidates.Count)];
        // The custom treasure placeholder is intentionally disabled until its
        // room and reward flow are implemented.
        var kind = CustomMapNodeKind.ShopLike;

        selected.PointType = kind == CustomMapNodeKind.ShopLike
            ? MapPointType.Shop
            : MapPointType.Treasure;

        nodesByAct[evt.ActIndex] = new Dictionary<MapCoord, CustomMapNodeKind>
        {
            [selected.coord] = kind,
        };

        Entry.Logger.Info($"[CustomMapNode] Act {evt.ActIndex + 1}: placed {kind} shell at {selected.coord}.");
    }
}
