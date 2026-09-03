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
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using RabbitAndSteelNewMap.Scripts.Power;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Jay : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("JAY.name");
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 41, 45);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 47);
    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/Jay.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(controller,
            idleName: "idle_loop", deadName: "die", hitName: "hurt",
            attackName: "attack", castName: "power", relaxedName: "idle_loop");

    private int WarmupVigor => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 2, 1);
    private int PlayDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);
    private int EchoDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 15);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var still = new MoveState("STILL_MOVE", StillMove, new DebuffIntent());
        var movement = new MoveState("MOVEMENT_MOVE", MovementMove, new DebuffIntent());
        var warmup = new MoveState("WARMUP_MOVE", WarmupMove, new BuffIntent());
        var play = new MoveState("PLAY_MOVE", PlayMove, new MultiAttackIntent(PlayDamage, 4));
        var echo = new MoveState("ECHO_MOVE", EchoMove, new SingleAttackIntent(EchoDamage), new DebuffIntent());
        var random = new RandomBranchState("RANDOM_A");
        random.AddBranch(still, MoveRepeatType.CanRepeatForever);
        random.AddBranch(movement, MoveRepeatType.CanRepeatForever);

        warmup.FollowUpState = echo;
        echo.FollowUpState = random;
        still.FollowUpState = play;
        movement.FollowUpState = play;
        play.FollowUpState = warmup;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { still, movement, warmup, play, echo, random },
            warmup);
    }

    private async Task StillMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StillPower>(new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
    }

    private async Task MovementMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<MovementPower>(new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
    }

    private async Task WarmupMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), Creature, WarmupVigor, Creature, null, false);
    }

    private async Task PlayMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(PlayDamage)
            .WithHitCount(4)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task EchoMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(EchoDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await PowerCmd.Apply<WeakPower>(
            new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null, false);
    }
}
