using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class AvyBoss : ModEncounterTemplate
{
    // NBossMapPoint uses <base>.png and <base>_outline.png for non-Spine boss icons.
    internal const string BossNodeBasePath =
        "res://mod/Iamge/Boss/EmeraldLakeside";
    private const string BossNodeIconPath = BossNodeBasePath + ".png";
    private const string BossNodeOutlinePath = BossNodeBasePath + "_outline.png";

    public override EncounterAssetProfile AssetProfile => new(
        MapNodeAssetPaths: [BossNodeIconPath, BossNodeOutlinePath],
        RunHistoryIconPath: BossNodeIconPath,
        RunHistoryIconOutlinePath: BossNodeOutlinePath);

    public override RoomType RoomType => RoomType.Boss;

    public override bool IsWeak => false;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<Avy>() };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<Avy>().ToMutable(), null)
        };
}
