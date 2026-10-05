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
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Ran : ModMonsterTemplate, IAttackSideIntentProvider
{
    private bool _isTransformed;

    public override LocString Title => MonsterModel.L10NMonsterLookup("RAN.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 60, 58);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 62, 59);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Ran.tscn");

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

    private int HeavyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int BreakDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);

    private int GuardBlock => 8;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var transform = new MoveState(
            "TRANSFORM_MOVE",
            TransformMove,
            new BuffIntent());
        var heavy = new MoveState(
            "HEAVY_MOVE",
            HeavyMove,
            new SingleAttackIntent(HeavyDamage));
        var breakMove = new MoveState(
            "BREAK_MOVE",
            BreakMove,
            new SingleAttackIntent(BreakDamage),
            new DebuffIntent(false));
        var afterHeavy = new ConditionalBranchState("AFTER_HEAVY_SIDE_CHECK");
        var afterBreak = new ConditionalBranchState("AFTER_BREAK_SIDE_CHECK");
        var afterTransform = new ConditionalBranchState("AFTER_TRANSFORM_SIDE_CHECK");
        var empower = new MoveState(
            "EMPOWER_MOVE",
            EmpowerMove,
            new BuffIntent());
        var reform = new MoveState(
            "REFORM_MOVE",
            ReformMove,
            new DefendIntent());

        transform.FollowUpState = afterTransform;
        empower.FollowUpState = reform;
        reform.FollowUpState = heavy;

        heavy.FollowUpState = afterHeavy;
        breakMove.FollowUpState = afterBreak;
        afterTransform.AddState(empower, () =>
            AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterTransform.AddState(breakMove, () =>
            !AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterHeavy.AddState(empower, () =>
            AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterHeavy.AddState(breakMove, () =>
            !AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterBreak.AddState(empower, () =>
            AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));
        afterBreak.AddState(heavy, () =>
            !AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature));

        return new MonsterMoveStateMachine(
            new List<MonsterState>
            {
                transform, heavy, breakMove, afterTransform, afterHeavy, afterBreak,
                empower, reform
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

        bool facingOwnSide = direction == AttackSideFacingPower.Direction.Left;
        string currentMoveId = monster.NextMove.Id;

        if (facingOwnSide &&
            (currentMoveId == "HEAVY_MOVE" || currentMoveId == "BREAK_MOVE"))
        {
            monster.SetMoveImmediate(
                (MoveState)moveStateMachine.States["EMPOWER_MOVE"]!,
                true);
        }
        else if (!facingOwnSide && currentMoveId == "EMPOWER_MOVE")
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
        await PowerCmd.Apply<AttackSideLeftPower>(
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

    private async Task HeavyMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(HeavyDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
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
            await PowerCmd.Apply<VulnerablePower>(
                new ThrowingPlayerChoiceContext(),
                players,
                1m,
                Creature,
                null,
                false);
        }
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var enemies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies == null)
            return;

        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            enemies,
            2m,
            Creature,
            null,
            false);
    }

    private async Task ReformMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var enemies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies == null)
            return;

        if (AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 1, 0) > 0)
        {
            foreach (var enemy in enemies)
                await CreatureCmd.GainBlock(
                    enemy,
                    GuardBlock,
                    ValueProp.Unpowered,
                    null,
                    false);
        }
        else
        {
            await CreatureCmd.GainBlock(
                Creature,
                GuardBlock,
                ValueProp.Unpowered,
                null,
                false);
        }
    }
}
