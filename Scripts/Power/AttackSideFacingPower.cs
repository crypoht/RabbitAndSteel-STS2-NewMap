using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class AttackSideFacingPower : ModPowerTemplate
{
    public enum Direction
    {
        Right,
        Left
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public Direction Facing { get; private set; } = Direction.Right;

    public async Task InitializeDefaultDirection()
    {
        await FaceDirection(Direction.Right);
        await EnsureFacingHasLivingEnemy();
    }

    public static bool IsPlayerFacingCreatureSide(Creature creature)
    {
        Direction? creatureSide = null;
        if (creature.HasPower<AttackSideLeftPower>())
            creatureSide = Direction.Left;
        else if (creature.HasPower<AttackSideRightPower>())
            creatureSide = Direction.Right;

        if (creatureSide == null)
            return false;

        return creature.CombatState?
                   .GetCreaturesOnSide(CombatSide.Player)
                   .Any(player =>
                       player.GetPower<AttackSideFacingPower>()?.Facing ==
                       creatureSide.Value)
               == true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || cardPlay.Target == null)
            return;

        await UpdateDirection(cardPlay.Target);
    }

    public override async Task BeforePotionUsed(
        PotionModel potion,
        Creature? target)
    {
        if (target != null && potion.Owner == Owner.Player)
            await UpdateDirection(target);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature.Side == Owner.Side)
            return;

        await EnsureFacingHasLivingEnemy();
    }

    private async Task UpdateDirection(Creature target)
    {
        if (Facing == Direction.Right &&
            target.HasPower<AttackSideLeftPower>())
        {
            await FaceDirection(Direction.Left);
        }
        else if (Facing == Direction.Left &&
                 target.HasPower<AttackSideRightPower>())
        {
            await FaceDirection(Direction.Right);
        }
    }

    private async Task EnsureFacingHasLivingEnemy()
    {
        var enemies = Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
            ?? Enumerable.Empty<Creature>();

        bool hasFacingEnemy = enemies.Any(enemy =>
            enemy.IsAlive &&
            ((Facing == Direction.Left && enemy.HasPower<AttackSideLeftPower>()) ||
             (Facing == Direction.Right && enemy.HasPower<AttackSideRightPower>())));
        if (hasFacingEnemy)
            return;

        var opposite = Facing == Direction.Right ? Direction.Left : Direction.Right;
        bool hasOppositeEnemy = enemies.Any(enemy =>
            enemy.IsAlive &&
            ((opposite == Direction.Left && enemy.HasPower<AttackSideLeftPower>()) ||
             (opposite == Direction.Right && enemy.HasPower<AttackSideRightPower>())));
        if (hasOppositeEnemy)
            await FaceDirection(opposite);
    }

    private async Task FaceDirection(Direction direction)
    {
        Facing = direction;

        var ownerAndPets = new[] { Owner }.Concat(Owner.Pets);
        foreach (var creature in ownerAndPets)
        {
            var body = NCombatRoom.Instance?.GetCreatureNode(creature)?.Body;
            if (body == null)
                continue;

            if ((direction == Direction.Right && body.Scale.X < 0f) ||
                (direction == Direction.Left && body.Scale.X > 0f))
            {
                body.Scale *= new Vector2(-1f, 1f);
            }
        }

        foreach (var enemy in Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy) ??
                 Enumerable.Empty<Creature>())
        {
            var combatState = enemy.CombatState;
            if (combatState == null ||
                !enemy.IsAlive ||
                (!enemy.HasPower<AttackSideLeftPower>() &&
                 !enemy.HasPower<AttackSideRightPower>()))
                continue;

            if (NCombatRoom.Instance?.GetCreatureNode(enemy) != null)
            {
                if (enemy.Monster is IAttackSideIntentProvider intentProvider)
                    intentProvider.RefreshAttackSideIntent(direction);

                enemy.PrepareForNextTurn(
                    combatState.PlayerCreatures,
                    rollNewMove: false);
            }
        }
    }
}
