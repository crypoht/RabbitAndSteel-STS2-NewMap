using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Act;

public sealed class KingdomAct : ModActTemplate
{
    protected override int BaseNumberOfRooms => 14;
    public override int Index => 1;
    public override bool IsDefault => false;
    public override ActAssetProfile AssetProfile => ContentAssetProfiles.FromVanillaActId("hive");
    public override string ChestSpineSkinNameNormal => "chest_room_act_hive_skel_data";
    public override string ChestSpineSkinNameStroke => "chest_room_act_hive_skel_data";
    public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act2";
    public override string AmbientSfx => "event:/sfx/ambience/act2_ambience";
    public override Color MapBgColor => new("9B9562");
    public override Color MapUntraveledColor => new("6E7750");
    public override Color MapTraveledColor => new("27221C");
    public override string[] MusicBankPaths => ["res://banks/desktop/act2_a1.bank", "res://banks/desktop/act2_a2.bank"];
    public override IReadOnlyList<EncounterModel> BossDiscoveryOrder => [];
    public override IReadOnlyList<AncientEventModel> AllAncients => [];
    public override IReadOnlyList<EventModel> AllEvents => [];
    public override string[] BgMusicOptions => ["event:/music/act2_a1_v2", "event:/music/act2_a2_v2"];

    public override bool IsUnlocked(UnlockState unlockState) => true;

    protected override void ApplyActDiscoveryOrderModifications(UnlockState unlockState)
    {
    }

    public override MapPointTypeCounts GetMapPointTypes(Rng rng)
    {
        var restCount = rng.NextGaussianInt(6, 1, 6, 7);
        var unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(rng) - 1;
        return new MapPointTypeCounts(unknownCount, restCount);
    }

    public override IEnumerable<EncounterModel> GenerateAllEncounters()
    {
        return [];
    }

    public override AncientEventModel[] GetUnlockedAncients(UnlockState unlockState)
    {
        return [];
    }
}
