using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Map;

public sealed class MapUiPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<HorizontalMapLayoutPatch>();
        patcher.RegisterPatch<HorizontalMapPathPatch>();
        patcher.RegisterPatch<HorizontalMapScrollPatch>();
        patcher.RegisterPatch<HorizontalMapMousePatch>();
        patcher.RegisterPatch<HorizontalMapWheelPatch>();
        patcher.RegisterPatch<HorizontalMapControllerPatch>();
        patcher.RegisterPatch<HorizontalMapBackgroundPatch>();
        patcher.RegisterPatch<HorizontalMapDrawingsPatch>();
        patcher.RegisterPatch<HorizontalMapDrawingsStatePatch>();
        patcher.RegisterPatch<CustomMapNodeIconPatch>();
    }
}
