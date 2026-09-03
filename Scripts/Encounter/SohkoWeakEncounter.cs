using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Scaffolding.Content;
using SohkoMonster = RabbitAndSteelNewMap.Scripts.Monster.Sohko;

namespace RabbitAndSteelNewMap.Scripts.Encounter;

public sealed class SohkoWeak : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<SohkoMonster>() };

    protected override IReadOnlyList<ValueTuple<MonsterModel, string?>> GenerateMonsters()
    {
        return new[]
        {
            new ValueTuple<MonsterModel, string?>(
                ModelDb.Monster<SohkoMonster>().ToMutable(),
                null)
        };
    }
}
