using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class MattiBossEncounter : ModEncounterTemplate
{
    internal const string BossNodeBasePath =
        "res://mod/Iamge/Boss/spr_menu_campaign_icon_4";
    private const string BossNodeIconPath = BossNodeBasePath + ".png";
    private const string BossNodeOutlinePath = BossNodeBasePath + "_outline.png";

    public override RoomType RoomType => RoomType.Boss;
    public override bool IsWeak => false;

    public override string? CustomEncounterScenePath =>
        "res://mod/Encounter/MattiBossEncounter.tscn";

    public override EncounterAssetProfile AssetProfile => new(
        MapNodeAssetPaths:
        [
            BossNodeIconPath, BossNodeOutlinePath
        ],
        RunHistoryIconPath: BossNodeIconPath,
        RunHistoryIconOutlinePath: BossNodeOutlinePath);

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<Matti>() };

    public override IReadOnlyList<string> Slots =>
        new[]
        {
            "matti",
            "aus_small",
            "ita_small",
            "meg_small",
            "nimi_small",
            "orn_small",
            "pine_small",
            "varo_small"
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<Matti>().ToMutable(), "matti")
        };
}
