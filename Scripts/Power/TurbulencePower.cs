using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class TurbulencePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => int.Max(0, Amount - GetInternalData<Data>().TriggersThisTurn);

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (DisplayAmount <= 0 || Owner.Player == null || cardPlay.Card.Owner != Owner.Player || !cardPlay.IsLastInSeries)
            return;

        Flash();

        var hand = PileType.Hand.GetPile(Owner.Player);
        var cardToDiscard = hand.Cards.Any()
            ? Owner.Player.RunState.Rng.CombatCardSelection.NextItem<CardModel>(hand.Cards)
            : null;

        if (cardToDiscard != null)
            await CardCmd.Discard(choiceContext, new[] { cardToDiscard });

        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player, false);

        SetTriggersThisTurn(GetInternalData<Data>().TriggersThisTurn + 1);
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
            SetTriggersThisTurn(0);

        return Task.CompletedTask;
    }

    private void SetTriggersThisTurn(int value)
    {
        GetInternalData<Data>().TriggersThisTurn = value;
        InvokeDisplayAmountChanged();
    }

    private sealed class Data
    {
        public int TriggersThisTurn;
    }
}
