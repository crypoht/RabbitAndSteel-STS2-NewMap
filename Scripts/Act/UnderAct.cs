using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Act;

public sealed class UnderAct : ModActTemplate
{
    private static Hive VanillaContent => ModelDb.Act<Hive>();

    protected override int NumberOfWeakEncounters => 2;
    protected override int BaseNumberOfRooms => 14;
    public override int Index => 2;
    public override bool IsDefault => false;
    public override ActAssetProfile AssetProfile => ContentAssetProfiles.FromVanillaActId("hive");
    public override string ChestSpineSkinNameNormal => "act2";
    public override string ChestSpineSkinNameStroke => "act2_stroke";
    public override string ChestOpenSfx => VanillaContent.ChestOpenSfx;
    public override string AmbientSfx => VanillaContent.AmbientSfx;
    public override Color MapBgColor => new("9B9562");
    public override Color MapUntraveledColor => new("6E7750");
    public override Color MapTraveledColor => new("27221C");
    public override string[] MusicBankPaths => VanillaContent.MusicBankPaths;
    public override IEnumerable<EncounterModel> BossDiscoveryOrder => VanillaContent.BossDiscoveryOrder;
    public override IEnumerable<AncientEventModel> AllAncients => VanillaContent.AllAncients;
    public override IEnumerable<EventModel> AllEvents => VanillaContent.AllEvents;
    public override string[] BgMusicOptions => VanillaContent.BgMusicOptions;

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
        return VanillaContent.GenerateAllEncounters();
    }

    public override IEnumerable<AncientEventModel> GetUnlockedAncients(UnlockState unlockState)
    {
        return VanillaContent.AllAncients;
    }
}
