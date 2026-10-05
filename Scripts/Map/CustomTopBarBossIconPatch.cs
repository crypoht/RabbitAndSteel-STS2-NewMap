using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;
using RabbitAndSteelNewMap.Scripts.Encounter;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Map;

public sealed class CustomTopBarBossIconPatch : IPatchMethod
{
    public static string PatchId => "custom_top_bar_boss_icons";
    public static bool IsCritical => true;
    public static string Description => "Use custom boss textures in the top bar as well as the map";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NTopBarBossIcon), "RefreshBossIcon")];

    public static void Postfix(NTopBarBossIcon __instance, IRunState ____runState,
        TextureRect ____bossIcon, TextureRect ____bossIconOutline,
        TextureRect? ____secondBossIcon, TextureRect? ____secondBossIconOutline)
    {
        var secondOnly = MapPatchReflection.GetProperty<bool>(__instance, "ShouldOnlyShowSecondBossIcon");
        SetIcon(secondOnly ? ____runState.Act.SecondBossEncounter : ____runState.Act.BossEncounter,
            ____bossIcon, ____bossIconOutline);
        if (!secondOnly)
            SetIcon(____runState.Act.SecondBossEncounter, ____secondBossIcon, ____secondBossIconOutline);
    }

    private static void SetIcon(EncounterModel? encounter, TextureRect? icon, TextureRect? outline)
    {
        var path = encounter switch
        {
            AvyBoss => AvyBoss.BossNodeBasePath,
            MattiBossEncounter => MattiBossEncounter.BossNodeBasePath,
            MerranBossEncounter => MerranBossEncounter.BossNodeBasePath,
            _ => null,
        };
        if (path == null || icon == null)
            return;
        icon.Texture = ResourceLoader.Load<Texture2D>(path + ".png");
        if (outline != null)
            outline.Texture = ResourceLoader.Load<Texture2D>(path + "_outline.png");
    }
}
