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

public sealed class Mink : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("MINK.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 53, 51);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 55, 52);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Mink.tscn");

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

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<HowlPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    private int StrikeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    private int ReturnKillDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 11);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var strike = new MoveState(
            "STRIKE_MOVE",
            StrikeMove,
            new SingleAttackIntent(StrikeDamage));
        var turbulence = new MoveState(
            "TURBULENCE_MOVE",
            TurbulenceMove,
            new DebuffIntent());
        var returnKill = new MoveState(
            "RETURN_KILL_MOVE",
            ReturnKillMove,
            new SingleAttackIntent(ReturnKillDamage));

        strike.FollowUpState = turbulence;
        turbulence.FollowUpState = returnKill;
        returnKill.FollowUpState = strike;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { strike, turbulence, returnKill },
            strike);
    }

    private async Task StrikeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StrikeDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task TurbulenceMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<TurbulencePower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            1m,
            Creature,
            null,
            false);
    }

    private async Task ReturnKillMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ReturnKillDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
    }
}
