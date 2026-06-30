using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Act;

public sealed class UnderAct : ModActTemplate
{
    protected override int BaseNumberOfRooms => 15;
    public override int Index => 2;
    public override bool IsDefault => false;
    public override ActAssetProfile AssetProfile => ContentAssetProfiles.FromVanillaActId("ship");
    public override string ChestSpineSkinNameNormal => "chest_room_act_ship_skel_data";
    public override string ChestSpineSkinNameStroke => "chest_room_act_ship_skel_data";
    public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act1";
    public override string AmbientSfx => "event:/sfx/ambience/act3_ambience";
    public override Color MapBgColor => new("9F95A5");
    public override Color MapUntraveledColor => new("534A62");
    public override Color MapTraveledColor => new("180F24");
    public override string[] MusicBankPaths => ["res://banks/desktop/act1_b1.bank"];
    public override IReadOnlyList<EncounterModel> BossDiscoveryOrder => [];
    public override IReadOnlyList<AncientEventModel> AllAncients => [];
    public override IReadOnlyList<EventModel> AllEvents => [];
    public override string[] BgMusicOptions => ["event:/music/act1_b1_v1"];

    public override bool IsUnlocked(UnlockState unlockState) => true;

    protected override void ApplyActDiscoveryOrderModifications(UnlockState unlockState)
    {
    }

    public override MapPointTypeCounts GetMapPointTypes(Rng rng)
    {
        var restCount = rng.NextGaussianInt(7, 1, 6, 7);
        var unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(rng);
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
