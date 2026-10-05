using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using RabbitAndSteelNewMap.Scripts.Power;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Xin : ModMonsterTemplate, IAttackSideIntentProvider
{
    private bool _isTransformed;

    public override LocString Title => MonsterModel.L10NMonsterLookup("XIN.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 60, 58);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 62, 59);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Xin.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller)
    {
        var idle0 = new AnimState("idle_loop_0", true);
        var idle1 = new AnimState("idle_loop_1", true);
        var dead = new AnimState("die");

        var hit0 = new AnimState("hurt_0") { NextState = idle0 };
        var hit1 = new AnimState("hurt_1") { NextState = idle1 };
        var attack0 = new AnimState("attack") { NextState = idle0 };
        var attack1 = new AnimState("attack") { NextState = idle1 };
        var cast0 = new AnimState("power") { NextState = idle0 };
        var cast1 = new AnimState("power") { NextState = idle1 };

        var animator = new CreatureAnimator(idle0, controller);
        animator.AddAnyState("Idle", idle1, () => _isTransformed);
        animator.AddAnyState("Idle", idle0);
        animator.AddAnyState("Dead", dead);
        animator.AddAnyState("Hit", hit1, () => _isTransformed);
        animator.AddAnyState("Hit", hit0);
        animator.AddAnyState("Attack", attack1, () => _isTransformed);
        animator.AddAnyState("Attack", attack0);
        animator.AddAnyState("Cast", cast1, () => _isTransformed);
        animator.AddAnyState("Cast", cast0);
        animator.AddAnyState("Relaxed", idle1, () => _isTransformed);
        animator.AddAnyState("Relaxed", idle0);
        return animator;
    }

    private int ClawDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private int BreakDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var transform = new MoveState(
            "TRANSFORM_MOVE",
            TransformMove,
            new BuffIntent());
        var claw = new MoveState(
            "CLAW_MOVE",
            ClawMove,
            new MultiAttackIntent(ClawDamage, 2));
        var breakMove = new MoveState(
            "BREAK_MOVE",
            BreakMove,
            new SingleAttackIntent(BreakDamage),
            new DebuffIntent(false));
        var afterClaw = new ConditionalBranchState("AFTER_CLAW_SIDE_CHECK");
        var afterBreak = new ConditionalBranchState("AFTER_BREAK_SIDE_CHECK");
        var afterTransform = new ConditionalBranchState("AFTER_TRANSFORM_SIDE_CHECK");
        var reinforce = new MoveState(
            "REINFORCE_MOVE",
            ReinforceMove,
            new BuffIntent());
        var renew = new MoveState(
            "RENEW_MOVE",
            RenewMove,
            new BuffIntent());

        transform.FollowUpState = afterTransform;
        reinforce.FollowUpState = renew;
        renew.FollowUpState = claw;

        claw.FollowUpState = afterClaw;
        breakMove.FollowUpState = afterBreak;
        afterTransform.AddState(reinforce, () =>
            AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterTransform.AddState(breakMove, () =>
            !AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterClaw.AddState(reinforce, () =>
            AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterClaw.AddState(breakMove, () =>
            !AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterBreak.AddState(reinforce, () =>
            AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterBreak.AddState(claw, () =>
            !AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));

        return new MonsterMoveStateMachine(
            new List<MonsterState>
            {
                transform, claw, breakMove, afterTransform, afterClaw, afterBreak,
                reinforce, renew
            },
            transform);
    }

    public void RefreshAttackSideIntent(AttackSideFacingPower.Direction direction)
    {
        var monster = Creature.Monster;
        if (monster == null)
            return;
        var moveStateMachine = monster.MoveStateMachine;
        if (moveStateMachine == null)
            return;

        bool facingOwnSide = direction == AttackSideFacingPower.Direction.Right;
        string currentMoveId = monster.NextMove.Id;

        if (facingOwnSide &&
            (currentMoveId == "CLAW_MOVE" || currentMoveId == "BREAK_MOVE"))
        {
            monster.SetMoveImmediate(
                (MoveState)moveStateMachine.States["REINFORCE_MOVE"]!,
                true);
        }
        else if (!facingOwnSide && currentMoveId == "REINFORCE_MOVE")
        {
            monster.SetMoveImmediate(
                (MoveState)moveStateMachine.States["BREAK_MOVE"]!,
                true);
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var players = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Player);
        if (players == null)
            return;

        await PowerCmd.Apply<AttackSideFacingPower>(
            new ThrowingPlayerChoiceContext(),
            players,
            1m,
            Creature,
            null,
            false);
        await PowerCmd.Apply<AttackSideRightPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);

        // The default direction is right. Apply it once after the side marker
        // exists so the first visible intent uses the correct side.
        var facing = players[0].GetPower<AttackSideFacingPower>();
        if (facing != null)
            await facing.InitializeDefaultDirection();
    }

    private async Task TransformMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        _isTransformed = true;
        await CreatureCmd.TriggerAnim(Creature, "Idle", 0f);
    }

    private async Task ClawMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(ClawDamage)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);

    private async Task BreakMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BreakDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);

        var players = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Player);
        if (players != null)
        {
            await PowerCmd.Apply<WeakPower>(
                new ThrowingPlayerChoiceContext(),
                players,
                1m,
                Creature,
                null,
                false);
        }
    }

    private async Task ReinforceMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var enemies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies == null)
            return;

        await PowerCmd.Apply<PlatingPower>(
            new ThrowingPlayerChoiceContext(),
            enemies,
            2m,
            Creature,
            null,
            false);
    }

    private async Task RenewMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var enemies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies == null)
            return;

        if (AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 1, 0) > 0)
        {
            await PowerCmd.Apply<VigorPower>(
                new ThrowingPlayerChoiceContext(),
                enemies,
                1m,
                Creature,
                null,
                false);
        }
        else
        {
            await PowerCmd.Apply<VigorPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null,
                false);
        }
    }
}
