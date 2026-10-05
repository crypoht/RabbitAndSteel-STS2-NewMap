using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class RequiredColorIconRefreshPatch : IPatchMethod
{
    public static string PatchId => "required_color_icon_refresh";
    public static bool IsCritical => true;
    public static string Description => "Refresh the required-color icon before flashing its new value";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NPower), "OnDisplayAmountChanged")];

    public static void Prefix(NPower __instance, TextureRect ____icon, CpuParticles2D ____powerFlash)
    {
        if (__instance.Model is not RequiredColorPower power || !__instance.IsNodeReady())
            return;
        ____icon.Texture = power.Icon;
        ____powerFlash.Texture = power.BigIcon;
    }
}
