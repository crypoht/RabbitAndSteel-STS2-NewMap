using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class RandomAdaptationPower : ModPowerTemplate
{
    private const int CardsPerShuffle = 2;

    protected override object InitInternalData() => new Data();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var cardOwner = cardPlay.Card.Owner.Creature;
        if (Owner?.Monster?.MoveStateMachine is null || cardOwner is null || cardOwner.Side != CombatSide.Player)
            return;

        var data = GetInternalData<Data>();
        data.PlayedCards++;
        if (data.PlayedCards < CardsPerShuffle)
            return;

        data.PlayedCards = 0;
        await ShuffleNextMove(choiceContext);
    }

    private async Task ShuffleNextMove(PlayerChoiceContext choiceContext)
    {
        var monster = Owner?.Monster;
        if (monster?.MoveStateMachine is null)
            return;

        var randomGroup = monster.MoveStateMachine.States.TryGetValue("RANDOM_A", out var state)
            ? state as RandomBranchState
            : null;
        if (randomGroup is null)
            return;

        var currentMove = monster.NextMove;
        var availableMoves = new List<MoveState>();
        foreach (var branch in randomGroup.States)
        {
            if (monster.MoveStateMachine.States.TryGetValue(branch.stateId, out var branchState) &&
                branchState is MoveState moveState &&
                moveState != currentMove)
            {
                availableMoves.Add(moveState);
            }
        }

        if (availableMoves.Count == 0)
            return;

        var rng = monster.RunRng.MonsterAi;
        var nextMove = availableMoves[rng.NextInt(availableMoves.Count)];
        monster.SetMoveImmediate(nextMove, true);
        await Task.CompletedTask;
    }

    private sealed class Data
    {
        public int PlayedCards;
    }
}
