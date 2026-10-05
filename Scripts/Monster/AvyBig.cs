using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
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
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class AvyBig : ModMonsterTemplate
{
    private MoveState? _rewardState;
    private bool _callRewardQueued;

    public override LocString Title => MonsterModel.L10NMonsterLookup("AVY_BIG.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 150, 130);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 151, 134);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/AvyBig.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller)
    {
        var idle = new AnimState("idle_loop", true);
        var dead = new AnimState("die", false);
        var hit = new AnimState("hurt", false) { NextState = idle };
        var attack = new AnimState("attack", false) { NextState = idle };
        var cast = new AnimState("power", false) { NextState = idle };
        var revive = new AnimState("resurrection", false) { NextState = idle };

        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Idle", idle);
        animator.AddAnyState("Dead", dead);
        animator.AddAnyState("Hit", hit);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("Cast", cast);
        animator.AddAnyState("Relaxed", idle);
        animator.AddAnyState("Revive", revive);
        return animator;
    }

    private int DanceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int FinaleDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 75, 60);

    private int IdolBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 13, 11);

    private bool IsHighAscension =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 1, 0) > 0;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<FrogIdolPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    public async Task PlayPhaseTransition()
    {
        await CreatureCmd.TriggerAnim(Creature, "Revive", 0f);
        await Cmd.Wait(1f, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var stillIntents = new List<AbstractIntent> { new DebuffIntent() };
        if (IsHighAscension)
            stillIntents.Add(new DefendIntent());

        var movementIntents = new List<AbstractIntent> { new DebuffIntent() };
        if (IsHighAscension)
            movementIntents.Add(new SingleAttackIntent(11));

        var still = new MoveState("STILL_MOVE", StillMove, stillIntents.ToArray());
        var movement = new MoveState("MOVEMENT_MOVE", MovementMove, movementIntents.ToArray());
        var color = new MoveState("COLOR_MOVE", ColorMove, new DebuffIntent());
        var dance = new MoveState(
            "DANCE_MOVE",
            DanceMove,
            new MultiAttackIntent(DanceDamage, 2));
        var idol = new MoveState(
            "IDOL_MOVE",
            IdolMove,
            new BuffIntent(),
            new DefendIntent());
        var call = new MoveState("CALL_MOVE", CallMove, new DebuffIntent());
        var finale = new MoveState(
            "FINALE_MOVE",
            FinaleMove,
            new SingleAttackIntent(FinaleDamage));
        var reward = new MoveState("REWARD_MOVE", RewardMove, new BuffIntent());

        var randomControl = new RandomBranchState("RANDOM_CONTROL");
        randomControl.AddBranch(still, MoveRepeatType.CanRepeatForever);
        randomControl.AddBranch(movement, MoveRepeatType.CanRepeatForever);

        color.FollowUpState = idol;
        idol.FollowUpState = randomControl;
        still.FollowUpState = dance;
        movement.FollowUpState = dance;
        dance.FollowUpState = call;
        call.FollowUpState = finale;
        finale.FollowUpState = idol;
        reward.FollowUpState = idol;

        _rewardState = reward;
        return new MonsterMoveStateMachine(
            new List<MonsterState>
            {
                still, movement, color, dance, idol, call, finale, reward,
                randomControl
            },
            color);
    }

    public void ScheduleCallReward()
    {
        if (_callRewardQueued || Creature.IsDead || _rewardState is null)
            return;

        _callRewardQueued = true;
        SetMoveImmediate(_rewardState);
    }

    private async Task StillMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StillPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
        if (IsHighAscension)
            await CreatureCmd.GainBlock(Creature, 11, ValueProp.Unpowered, null, false);

        TalkCmd.Play(
            new LocString("monsters", "AVY_BIG.moves.STILL_MOVE.talk"),
            Creature,
            VfxColor.White,
            VfxDuration.Standard);
    }

    private async Task MovementMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<MovementPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
        if (IsHighAscension)
        {
            await DamageCmd.Attack(11)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f, null)
                .WithAttackerFx(null, AttackSfx, null)
                .WithHitFx("vfx/vfx_attack_blunt", null, null)
                .Execute(null);
        }

        TalkCmd.Play(
            new LocString("monsters", "AVY_BIG.moves.MOVEMENT_MOVE.talk"),
            Creature,
            VfxColor.White,
            VfxDuration.Standard);
    }

    private async Task ColorMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        if (Creature.GetPower<BlushColorPower>() == null)
        {
            await PowerCmd.Apply<BlushColorPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null,
                false);
        }
    }

    private async Task DanceMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(DanceDamage)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task IdolMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(), Creature, 7m, Creature, null, false);
        await CreatureCmd.GainBlock(Creature, IdolBlock, ValueProp.Unpowered, null, false);
    }

    private async Task CallMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<CallPower>(
            new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }

    private async Task FinaleMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(FinaleDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task RewardMove(IReadOnlyList<Creature> targets)
    {
        _callRewardQueued = false;
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), targets, 3m, Creature, null, false);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), Creature, 3m, Creature, null, false);
    }
}
