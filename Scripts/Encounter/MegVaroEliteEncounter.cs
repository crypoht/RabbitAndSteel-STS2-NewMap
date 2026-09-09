using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class MegVaroEliteEncounter : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Elite;

    public override bool IsWeak => false;

    public override string? CustomEncounterScenePath =>
        "res://mod/Encounter/MegVaroEliteEncounter.tscn";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Meg>(),
            ModelDb.Monster<Varo>()
        };

    public override IReadOnlyList<string> Slots => new[] { "meg", "varo" };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<Meg>().ToMutable(), "meg"),
            (ModelDb.Monster<Varo>().ToMutable(), "varo")
        };
}
