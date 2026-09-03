using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class JayLetteWeak : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool IsWeak => false;
    public override string? CustomEncounterScenePath => "res://mod/Encounter/JayLetteWeak.tscn";
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[] { ModelDb.Monster<Jay>(), ModelDb.Monster<Lette>() };
    public override IReadOnlyList<string> Slots => new[] { "first", "last" };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() => new (MonsterModel, string?)[]
    {
        (ModelDb.Monster<Jay>().ToMutable(), "first"),
        (ModelDb.Monster<Lette>().ToMutable(), "last")
    };
}
