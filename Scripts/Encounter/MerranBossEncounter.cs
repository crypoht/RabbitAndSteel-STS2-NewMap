using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class MerranBossEncounter : ModEncounterTemplate
{
    internal const string BossNodeBasePath =
        "res://mod/Iamge/Boss/spr_menu_campaign_icon_2";
    private const string BossNodeIconPath = BossNodeBasePath + ".png";
    private const string BossNodeOutlinePath = BossNodeBasePath + "_outline.png";

    public override RoomType RoomType => RoomType.Boss;
    public override bool IsWeak => false;
    public override bool FullyCenterPlayers => true;
    public override float GetCameraScaling() => 0.72f;
    public override Vector2 GetCameraOffset() => Vector2.Down * 25f;
    public override string? CustomEncounterScenePath => "res://mod/Encounter/MerranBossEncounter.tscn";
    public override EncounterAssetProfile AssetProfile => new(
        MapNodeAssetPaths:
        [
            BossNodeIconPath, BossNodeOutlinePath
        ],
        RunHistoryIconPath: BossNodeIconPath,
        RunHistoryIconOutlinePath: BossNodeOutlinePath);

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<Merran>() };

    public override IReadOnlyList<string> Slots =>
        new[] { "merran", "merran_phantom" };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<Merran>().ToMutable(), "merran")
        };
}
