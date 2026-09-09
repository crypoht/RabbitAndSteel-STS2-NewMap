using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class AusNimiStrongEncounter : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override string? CustomEncounterScenePath =>
        "res://mod/Encounter/AusNimiStrongEncounter.tscn";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Aus>(),
            ModelDb.Monster<Nimi>()
        };

    public override IReadOnlyList<string> Slots => new[] { "aus", "nimi" };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new (MonsterModel, string?)[]
        {
            (ModelDb.Monster<Aus>().ToMutable(), "aus"),
            (ModelDb.Monster<Nimi>().ToMutable(), "nimi")
        };
}
