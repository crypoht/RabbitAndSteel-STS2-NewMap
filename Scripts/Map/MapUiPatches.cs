using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Map;

public sealed class MapUiPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<CustomMapNodeIconPatch>();
        patcher.RegisterPatch<AvyBossMapNodePathPatch>();
    }
}
