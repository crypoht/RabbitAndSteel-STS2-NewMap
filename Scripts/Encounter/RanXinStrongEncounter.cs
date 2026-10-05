using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class RanXinStrongEncounter : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override string? CustomEncounterScenePath =>
        "res://mod/Encounter/RanXinStrongEncounter.tscn";

    public override bool FullyCenterPlayers => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Ran>(),
            ModelDb.Monster<Xin>()
        };

    public override IReadOnlyList<string> Slots => new[] { "ran", "xin" };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<Ran>().ToMutable(), "ran"),
            (ModelDb.Monster<Xin>().ToMutable(), "xin")
        };
}
