using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Map;

public sealed class CustomMapNodeIconPatch : IPatchMethod
{
    public static string PatchId => "rabbit_custom_map_node_icons";
    public static bool IsCritical => false;
    public static string Description => "Apply placeholder custom map node icon overrides";
    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NNormalMapPoint), "_Ready"),
        new(typeof(NNormalMapPoint), "UpdateIcon"),
    ];

    public static void Postfix(NNormalMapPoint __instance)
    {
        try
        {
            var runState = MapPatchReflection.GetField<IRunState>(__instance, "_runState");
            if (runState == null)
                return;

            var coord = __instance.Point.coord;
            if (!CustomMapNodeRegistry.TryGetKind(runState.CurrentActIndex, coord, out var kind))
                return;

            if (!CustomMapNodeRegistry.TryGetIconPaths(kind, out var iconPath, out var outlinePath))
                return;

            var icon = MapPatchReflection.GetField<TextureRect>(__instance, "_icon");
            var outline = MapPatchReflection.GetField<TextureRect>(__instance, "_outline");

            var iconTexture = LoadTextureOrNull(iconPath);
            if (iconTexture != null && icon != null)
                icon.Texture = iconTexture;

            var outlineTexture = LoadTextureOrNull(outlinePath);
            if (outlineTexture != null && outline != null)
                outline.Texture = outlineTexture;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[CustomMapNode] Icon patch failed: {ex}");
        }
    }

    private static Texture2D? LoadTextureOrNull(string path)
    {
        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
    }
}
