using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class MattiBossEncounter : ModEncounterTemplate
{
    private const string BossNodeIconPath =
        "res://mod/Iamge/Boss/spr_menu_campaign_icon_4.png";

    public override RoomType RoomType => RoomType.Boss;
    public override bool IsWeak => false;

    public override string? CustomEncounterScenePath =>
        "res://mod/Encounter/MattiBossEncounter.tscn";

    public override EncounterAssetProfile AssetProfile => new(
        MapNodeAssetPaths:
        [
            BossNodeIconPath
        ],
        RunHistoryIconPath: BossNodeIconPath,
        RunHistoryIconOutlinePath: BossNodeIconPath);

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
