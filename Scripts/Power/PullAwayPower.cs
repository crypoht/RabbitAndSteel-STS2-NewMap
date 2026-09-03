using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class PullAwayPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DynamicVar("DamageReduction", 30m) };

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || dealer?.Side != CombatSide.Enemy ||
            !props.IsPoweredAttack())
            return 1m;

        return (decimal)Math.Pow(0.7, Amount);
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            DynamicVars["DamageReduction"].BaseValue = GetDamageReductionPercent();
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player)
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(this);
    }

    private decimal GetDamageReductionPercent() =>
        decimal.Truncate((1m - (decimal)Math.Pow(0.7, Amount)) * 100m);
}
