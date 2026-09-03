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
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Rem : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("REM.name");

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 58, 50);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 62, 56);

    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/rem.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);
    }

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller)
    {
        return ModAnimStateMachines.Standard(
            controller,
            idleName: "idle_loop",
            deadName: "die",
            hitName: "hurt",
            attackName: "attack",
            castName: "power",
            relaxedName: "idle_loop");
    }

    private int DashDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 10);

    private int BiteDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var dash = new MoveState("DASH_MOVE", DashMove, new AbstractIntent[]
        {
            new SingleAttackIntent(DashDamage)
        });
        var bite = new MoveState("BITE_MOVE", BiteMove, new AbstractIntent[]
        {
            new SingleAttackIntent(BiteDamage),
            new DebuffIntent(true)
        });
        var trap = new MoveState("TRAP_MOVE", TrapMove, new AbstractIntent[]
        {
            new DebuffIntent(true),
            new BuffIntent()
        });

        trap.FollowUpState = dash;
        dash.FollowUpState = bite;
        bite.FollowUpState = trap;

        return new MonsterMoveStateMachine(new List<MonsterState> { trap, dash, bite }, trap);
    }

    private async Task DashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DashDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.95f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task BiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BiteDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.95f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }

    private async Task TrapMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<TurbulencePower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
    }
}
