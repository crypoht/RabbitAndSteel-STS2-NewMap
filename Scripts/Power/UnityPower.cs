using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class UnityPower : ModPowerTemplate
{
    private const string DamageReductionKey = "DamageReduction";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DynamicVar(DamageReductionKey, 80m) };

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner ||
            dealer?.Side != CombatSide.Player ||
            !props.IsPoweredAttack() ||
            !HasLivingAlly())
        {
            return 1m;
        }

        return Math.Max(0m, 1m - Amount * 0.1m);
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
        {
            DynamicVars[DamageReductionKey].BaseValue =
                Math.Clamp(Amount * 10m, 0m, 100m);
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Enemy ||
            Owner is null ||
            !Owner.IsAlive ||
            !participants.Contains(Owner))
        {
            return;
        }

        foreach (var enemy in combatState.GetCreaturesOnSide(CombatSide.Enemy))
        {
            if (enemy != Owner && enemy.IsAlive)
                await CreatureCmd.GainBlock(
                    enemy,
                    decimal.ToInt32(Amount),
                    ValueProp.Unpowered,
                    null,
                    false);
        }
    }

    private bool HasLivingAlly() =>
        Owner?.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
            .Any(creature => creature != Owner && creature.IsAlive) == true;
}
