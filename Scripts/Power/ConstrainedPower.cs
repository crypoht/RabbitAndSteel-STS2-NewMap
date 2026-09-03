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
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using RabbitAndSteelNewMap.Scripts.Affliction;
using RabbitAndSteelNewMap.Scripts.Keyword;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class ConstrainedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public static async Task ApplyToPlayer(Player player, Creature applier, int cardCount = 3)
    {
        var draw = PileType.Draw.GetPile(player).Cards;
        var discard = PileType.Discard.GetPile(player).Cards;
        var candidates = draw.Concat(discard)
            .Where(IsEligibleCard)
            .ToList();
        var preferred = candidates.Where(card =>
            card.Type is CardType.Attack or CardType.Skill or CardType.Power).ToList();
        var selected = (preferred.Count > 0 ? preferred : candidates)
            .Take(cardCount)
            .ToList();
        if (selected.Count == 0)
        {
            Entry.Logger.Info("[Constrained] No eligible cards were available to move into hand.");
            return;
        }

        var addResults = await CardPileCmd.Add(
            selected, PileType.Hand, CardPilePosition.Bottom, null, false);
        var addedToHand = addResults
            .Where(result => result.success &&
                             result.cardAdded.Pile?.Type == PileType.Hand)
            .Select(result => result.cardAdded)
            .ToList();

        var constrainedCards = new List<CardModel>();
        foreach (var card in addedToHand)
        {
            CardCmd.ApplyKeyword(card, ConstrainedKeywords.Constrained);
            CardCmd.ApplySingleTurnRetain(card);
            constrainedCards.Add(card);
        }

        Entry.Logger.Info(
            $"[Constrained] Requested={cardCount}, selected={selected.Count}, " +
            $"addedToHand={addedToHand.Count}, constrained={constrainedCards.Count}; " +
            $"cards=[{string.Join(", ", constrainedCards.Select(card => card.Id.Entry))}]");

        if (constrainedCards.Count == 0)
            return;

        var constrainedPower = await PowerCmd.Apply<ConstrainedPower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            constrainedCards.Count,
            applier,
            null,
            false);
        if (constrainedPower is null)
        {
            Entry.Logger.Warn("[Constrained] Failed to apply player power.");
            return;
        }

        constrainedPower.LockNextPlayerTurn(player.PlayerCombatState?.TurnNumber ?? 0);
        Entry.Logger.Info(
            $"[Constrained] Applied player power; amount={constrainedPower.Amount}, lockedThroughTurn=" +
            $"{constrainedPower.GetInternalData<Data>().LockedThroughTurn}.");
        player.PlayerCombatState?.RecalculateCardValues();
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        var data = GetInternalData<Data>();
        if (card.Owner.Creature != Owner || !keywords.Contains(ConstrainedKeywords.Constrained) ||
            !IsLockedForCurrentTurn(data))
            return false;

        return keywords.Add(CardKeyword.Unplayable);
    }

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        var data = GetInternalData<Data>();
        if (IsLockedForCurrentTurn(data) || !data.Released || data.Played ||
            card.Owner.Creature != Owner ||
            !HasConstrainedKeyword(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        var data = GetInternalData<Data>();
        if (card.Owner.Creature != Owner || !HasConstrainedKeyword(card))
            return true;

        return !IsLockedForCurrentTurn(data) && data.Released && !data.Played;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        if (cardPlay.Card.Owner.Creature == Owner &&
            HasConstrainedKeyword(cardPlay.Card) && data.Released)
        {
            data.Played = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        if (!data.Played || cardPlay.Card.Owner.Creature != Owner ||
            !HasConstrainedKeyword(cardPlay.Card))
        {
            return;
        }

        var player = Owner.Player;
        if (player == null)
            return;

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null)
            return;

        var constrainedCards = playerCombatState.AllCards
            .Where(HasConstrainedKeyword)
            .ToList();

        foreach (var card in constrainedCards)
            CardCmd.RemoveKeyword(card, ConstrainedKeywords.Constrained);

        var remainingCards = constrainedCards
            .Where(card => card != cardPlay.Card && card.Pile?.Type == PileType.Hand)
            .ToList();
        if (remainingCards.Count > 0)
            await CardCmd.Discard(choiceContext, remainingCards);

        await PowerCmd.Remove(this);
    }

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner)
            return Task.CompletedTask;

        var data = GetInternalData<Data>();
        var turnNumber = player.PlayerCombatState?.TurnNumber ?? 0;
        if (turnNumber <= data.LockedThroughTurn)
        {
            if (turnNumber == data.LockedThroughTurn &&
                data.RetainAppliedForTurn != turnNumber)
            {
                data.RetainAppliedForTurn = turnNumber;
                foreach (var card in player.PlayerCombatState?.AllCards
                             .Where(HasConstrainedKeyword) ??
                         Enumerable.Empty<CardModel>())
                {
                    card.GiveSingleTurnRetain();
                }

                return Task.CompletedTask;
            }
        }

        data.Released = true;
        player.PlayerCombatState?.RecalculateCardValues();
        return Task.CompletedTask;
    }

    public void LockNextPlayerTurn(int currentTurn)
    {
        var data = GetInternalData<Data>();
        data.LockedThroughTurn = currentTurn + 1;
        data.RetainAppliedForTurn = -1;
        data.Released = false;
        data.Played = false;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner))
            return;

        var player = Owner.Player;
        if (player == null)
            return;

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null)
            return;

        if (!GetInternalData<Data>().Released)
            return;

        var constrainedCards = playerCombatState.AllCards
            .Where(HasConstrainedKeyword)
            .ToList();
        foreach (var card in constrainedCards)
        {
            CardCmd.RemoveKeyword(card, ConstrainedKeywords.Constrained);
        }

        await PowerCmd.Remove(this);
    }

    private bool IsLockedForCurrentTurn(Data data)
    {
        if (data.Released)
            return false;

        var turnNumber = Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;
        return turnNumber <= data.LockedThroughTurn;
    }

    private static bool IsEligibleCard(CardModel card)
    {
        return !HasConstrainedKeyword(card)
               && card.Type is not CardType.Status and not CardType.Curse
               && !card.Keywords.Contains(CardKeyword.Unplayable);
    }

    private static bool HasConstrainedKeyword(CardModel card)
    {
        return card.GetKeywordsWithSources(KeywordSources.Local)
            .Contains(ConstrainedKeywords.Constrained);
    }

    private sealed class Data
    {
        public int LockedThroughTurn;
        public int RetainAppliedForTurn = -1;
        public bool Released;
        public bool Played;
    }
}
