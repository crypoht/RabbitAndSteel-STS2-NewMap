using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class MerranAttentionPower : ModPowerTemplate
{
    private const string DamageReductionKey = "DamageReduction";
    private const decimal DamageReductionPerStack = 0.15m;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Math.Max(0, Amount - 1);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DynamicVar(DamageReductionKey, 0m) };

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner || dealer?.Side != CombatSide.Enemy || !props.IsPoweredAttack())
            return 1m;

        return Math.Max(0m, 1m - DamageReductionPerStack * Math.Max(0, Amount - 1));
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
                decimal.Truncate(DamageReductionPerStack * Math.Max(0, Amount - 1) * 100m);
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner.Player == null || cardPlay.Card.Owner != Owner.Player ||
            cardPlay.Card.Type != CardType.Attack || cardPlay.Target == null ||
            !IsCorrectSide(Owner, cardPlay.Target))
        {
            await RefreshSideMarker(Owner);
            return;
        }

        await PowerCmd.Apply<MerranAttentionPower>(
            choiceContext,
            Owner,
            1m,
            Owner,
            null,
            false);
        await RefreshSideMarker(Owner);
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner || Amount <= 1)
            return;

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -(Amount - 1),
            null,
            null,
            false);
    }

    private static bool IsCorrectSide(Creature player, Creature target) =>
        target.SlotName?.Contains("phantom", StringComparison.OrdinalIgnoreCase) == true
            ? player.HasPower<MerranAttackSideLeftPower>()
            : player.HasPower<MerranAttackSideRightPower>();

    private static async Task RefreshSideMarker(Creature player)
    {
        await PowerCmd.Remove<MerranAttackSideLeftPower>(player);
        await PowerCmd.Remove<MerranAttackSideRightPower>(player);

        if (player.Player?.RunState.Rng.CombatTargets.NextBool() == true)
        {
            await PowerCmd.Apply<MerranAttackSideLeftPower>(
                new ThrowingPlayerChoiceContext(), player, 1m, player, null, false);
        }
        else
        {
            await PowerCmd.Apply<MerranAttackSideRightPower>(
                new ThrowingPlayerChoiceContext(), player, 1m, player, null, false);
        }
    }
}
