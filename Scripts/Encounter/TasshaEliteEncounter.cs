using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class TasshaEliteEncounter : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Elite;
    public override bool IsWeak => false;
    public override bool FullyCenterPlayers => true;
    public override float GetCameraScaling() => 0.75f;
    public override Vector2 GetCameraOffset() => Vector2.Down * 35f;
    public override string? CustomEncounterScenePath => "res://mod/Encounter/TasshaEliteEncounter.tscn";
    public override EncounterAssetProfile AssetProfile => new(
        MapNodeAssetPaths: ["res://mod/Iamge/Boss/EmeraldLakeside.png", "res://mod/Iamge/Boss/EmeraldLakeside_outline.png"],
        RunHistoryIconPath: "res://mod/Iamge/Boss/EmeraldLakeside.png",
        RunHistoryIconOutlinePath: "res://mod/Iamge/Boss/EmeraldLakeside_outline.png");
    public override IEnumerable<MonsterModel> AllPossibleMonsters => new[] { ModelDb.Monster<Tassha>() };
    public override IReadOnlyList<string> Slots => new[] { "tassha", "tassha_phantom_left_1", "tassha_phantom_left_2", "tassha_phantom_right_1" };
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => new[] { (ModelDb.Monster<Tassha>().ToMutable(), (string?)"tassha") };
}
