using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class MovementPower : ModPowerTemplate
{
    private const string DamageDecreaseKey = "DamageDecrease";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DynamicVar(DamageDecreaseKey, 0m) };

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner || dealer?.Side != CombatSide.Enemy || !props.IsPoweredAttack())
            return 0m;

        return -DynamicVars[DamageDecreaseKey].BaseValue;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
            return Task.CompletedTask;

        DynamicVars[DamageDecreaseKey].BaseValue += 1m;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Decrement(this);
    }
}
