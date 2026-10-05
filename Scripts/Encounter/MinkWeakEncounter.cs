using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Scaffolding.Content;
using MinkMonster = RabbitAndSteelNewMap.Scripts.Monster.Mink;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class MinkWeak : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool IsWeak => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<MinkMonster>() };

    protected override IReadOnlyList<ValueTuple<MonsterModel, string?>> GenerateMonsters()
    {
        return new[]
        {
            new ValueTuple<MonsterModel, string?>(
                ModelDb.Monster<MinkMonster>().ToMutable(),
                null)
        };
    }
}
