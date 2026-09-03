using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class ItaPineWeak : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool IsWeak => true;
    public override string? CustomEncounterScenePath =>
        "res://mod/Encounter/ItaPineWeak.tscn";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[] { ModelDb.Monster<Pine>(), ModelDb.Monster<Ita>() };

    public override IReadOnlyList<string> Slots => new[] { "first", "last" };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => new (MonsterModel, string?)[]
    {
        (ModelDb.Monster<Pine>().ToMutable(), "first"),
        (ModelDb.Monster<Ita>().ToMutable(), "last")
    };
}
