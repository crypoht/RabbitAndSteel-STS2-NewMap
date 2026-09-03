using System.Collections.Generic;
using System.Linq;
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
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Avy : ModMonsterTemplate
{
    private MoveState? _rewardState;
    private MoveState? _reviveState;
    private bool _callRewardQueued;

    public override LocString Title => MonsterModel.L10NMonsterLookup("AVY.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 190, 181);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 194, 186);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Avy.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(
            controller,
            idleName: "idle_loop",
            deadName: "die",
            deadLoop: true,
            hitName: "hurt",
            attackName: "attack",
            castName: "power",
            relaxedName: "idle_loop");

    private int ColorBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 20, 14);

    private int DanceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 14);

    private int SingDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 88, 66);

    private int ChorusHits =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int PullDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 21, 18);

    private bool IsHighAscension =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 1, 0) > 0;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<AvyRevivalPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var constrain = new MoveState(
            "CONSTRAIN_MOVE",
            ConstrainMove,
            new DebuffIntent(),
            new BuffIntent());
        var still = new MoveState("STILL_MOVE", StillMove, new DebuffIntent());
        var movement = new MoveState("MOVEMENT_MOVE", MovementMove, new DebuffIntent());
        var color = new MoveState(
            "COLOR_MOVE",
            ColorMove,
            new DebuffIntent(),
            new DefendIntent());
        var dance = new MoveState(
            "DANCE_MOVE",
            DanceMove,
            new SingleAttackIntent(DanceDamage));
        var sing = new MoveState(
            "SING_MOVE",
            SingMove,
            new SingleAttackIntent(SingDamage));
        var chorus = new MoveState(
            "CHORUS_MOVE",
            ChorusMove,
            new MultiAttackIntent(5, ChorusHits));
        var pull = new MoveState(
            "PULL_MOVE",
            PullMove,
            new DebuffIntent(),
            new SingleAttackIntent(PullDamage));
        var call = new MoveState(
            "CALL_MOVE",
            CallMove,
            new DebuffIntent());
        var revive = new MoveState(
            "REVIVE_MOVE",
            ReviveMove,
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
        var reward = new MoveState(
            "REWARD_MOVE",
            RewardMove,
            new BuffIntent());

        var randomControl = new RandomBranchState("RANDOM_CONTROL");
        randomControl.AddBranch(still, MoveRepeatType.CannotRepeat);
        randomControl.AddBranch(movement, MoveRepeatType.CannotRepeat);

        var randomAttack = new RandomBranchState("RANDOM_ATTACK");
        randomAttack.AddBranch(dance, MoveRepeatType.CanRepeatForever);
        randomAttack.AddBranch(chorus, MoveRepeatType.CanRepeatForever);

        color.FollowUpState = constrain;
        constrain.FollowUpState = randomControl;
        still.FollowUpState = randomAttack;
        movement.FollowUpState = randomAttack;
        dance.FollowUpState = pull;
        chorus.FollowUpState = pull;
        pull.FollowUpState = call;
        call.FollowUpState = sing;
        sing.FollowUpState = constrain;
        reward.FollowUpState = constrain;

        _rewardState = reward;
        _reviveState = revive;

        return new MonsterMoveStateMachine(
            new List<MonsterState>
            {
                color, constrain, still, movement, dance, sing, chorus, pull, call,
                revive, reward, randomControl, randomAttack
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

    public void ScheduleRevive()
    {
        if (_reviveState != null)
            SetMoveImmediate(_reviveState, true);
    }

    private async Task ConstrainMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        foreach (var target in targets.Where(target => target.Player != null))
            await ConstrainedPower.ApplyToPlayer(target.Player!, Creature);

        if (IsHighAscension)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null,
                false);
        }
    }

    private async Task StillMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StillPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
        TalkCmd.Play(
            new LocString("monsters", "AVY.moves.STILL_MOVE.talk"),
            Creature,
            VfxColor.White,
            VfxDuration.Standard);
    }

    private async Task MovementMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<MovementPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
        TalkCmd.Play(
            new LocString("monsters", "AVY.moves.MOVEMENT_MOVE.talk"),
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

        if (IsHighAscension)
        {
            await PowerCmd.Apply<WeakPower>(
                new ThrowingPlayerChoiceContext(),
                targets,
                3m,
                Creature,
                null,
                false);
        }

        await CreatureCmd.GainBlock(Creature, ColorBlock, ValueProp.Unpowered, null, false);
    }

    private async Task DanceMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(DanceDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task SingMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(SingDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task ChorusMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(5)
            .WithHitCount(ChorusHits)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task PullMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PullDamage)
            .FromMonster(this)
            .WithAttackerAnim("Cast", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

        foreach (var target in targets.Where(target => target.Player != null))
            await ConstrainedPower.ApplyToPlayer(target.Player!, Creature);
    }

    private async Task CallMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<CallPower>(
            new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }

    private async Task RewardMove(IReadOnlyList<Creature> targets)
    {
        _callRewardQueued = false;
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null, false);
    }

    private async Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState;
        if (combatState is null)
            return;

        var combatRoom = NCombatRoom.Instance;
        var oldCreatureNode = combatRoom?.GetCreatureNode(Creature);
        var oldPosition = oldCreatureNode?.GlobalPosition;
        if (combatRoom != null && oldCreatureNode != null)
        {
            combatRoom.RemoveCreatureNode(oldCreatureNode);
            oldCreatureNode.QueueFree();
        }

        CombatManager.Instance.RemoveCreature(Creature);
        combatState.RemoveCreature(Creature, true);

        var secondPhase = await CreatureCmd.Add(
            ModelDb.Monster<AvyBig>().ToMutable(),
            combatState,
            Creature.Side,
            Creature.SlotName);

        // Dynamic spawns without encounter slots are not positioned by NCombatRoom.
        // Mirror the official replacement-spawn flow: hide, position, then reveal.
        secondPhase.SetNodeVisible(false);
        var secondPhaseNode = combatRoom?.GetCreatureNode(secondPhase);
        if (oldPosition is not null && secondPhaseNode != null)
            secondPhaseNode.GlobalPosition = oldPosition.Value;
        secondPhase.SetNodeVisible(true);

        if (secondPhase.Monster is AvyBig avyBig)
            await avyBig.PlayPhaseTransition();
    }
}
