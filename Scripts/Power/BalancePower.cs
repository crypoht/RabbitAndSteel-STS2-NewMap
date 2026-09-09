using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class BalancePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner is null || !participants.Contains(Owner))
            return;

        var combatState = Owner.CombatState;
        if (combatState is null)
            return;

        var balancedCreatures = combatState
            .GetCreaturesOnSide(CombatSide.Enemy)
            .Where(creature => creature.IsAlive && creature.GetPower<BalancePower>() is not null)
            .OrderBy(creature => creature.SlotName ?? string.Empty)
            .ThenBy(creature => creature.Monster?.Id.Entry ?? string.Empty)
            .ToList();

        if (balancedCreatures.Count <= 1 || balancedCreatures[0] != Owner)
            return;

        var balancedHp = Math.Ceiling(
            balancedCreatures.Sum(creature => creature.CurrentHp) / (decimal)balancedCreatures.Count);

        foreach (var creature in balancedCreatures)
        {
            var targetHp = Math.Min(balancedHp, creature.MaxHp);
            if (creature.CurrentHp != targetHp)
                await CreatureCmd.SetCurrentHp(creature, targetHp);
        }
    }
}
