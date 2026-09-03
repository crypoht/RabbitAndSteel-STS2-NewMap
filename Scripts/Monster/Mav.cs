using System.Collections.Generic;
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

public sealed class Mav : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("MAV.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 71, 61);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 80, 68);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Mav.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(
            controller,
            idleName: "idle_loop",
            deadName: "die",
            hitName: "hurt",
            attackName: "attack",
            castName: "power",
            relaxedName: "idle_loop");

    private int BallDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 20, 17);

    private int ForceDamage => 7;

    private int ForceBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 11, 8);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var still = new MoveState("STILL_MOVE", StillMove, new DebuffIntent());
        var movement = new MoveState("MOVEMENT_MOVE", MovementMove, new DebuffIntent());
        var ball = new MoveState("BALL_MOVE", BallMove, new SingleAttackIntent(BallDamage));
        var force = new MoveState(
            "FORCE_MOVE",
            ForceMove,
            new SingleAttackIntent(ForceDamage),
            new DefendIntent());

        force.FollowUpState = new RandomBranchState("RANDOM_A");
        var random = (RandomBranchState)force.FollowUpState;
        random.AddBranch(still, MoveRepeatType.CanRepeatForever);
        random.AddBranch(movement, MoveRepeatType.CanRepeatForever);
        still.FollowUpState = ball;
        movement.FollowUpState = ball;
        ball.FollowUpState = force;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { still, movement, ball, force, random },
            force);
    }

    private async Task StillMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StillPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
        TalkCmd.Play(
            new LocString("monsters", "MAV.moves.STILL_MOVE.talk"),
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
            new LocString("monsters", "MAV.moves.MOVEMENT_MOVE.talk"),
            Creature,
            VfxColor.White,
            VfxDuration.Standard);
    }

    private async Task BallMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BallDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
    }

    private async Task ForceMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ForceDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, ForceBlock, ValueProp.Unpowered, null, false);
    }
}
