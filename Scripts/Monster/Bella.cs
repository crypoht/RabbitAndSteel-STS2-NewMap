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
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Bella : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("BELLA.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 42, 35);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 50, 41);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Bella.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);
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

    private int SlapDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    private int SmashDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);

    private int GuardBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 12, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var slap = new MoveState("SLAP_MOVE", SlapMove, new AbstractIntent[]
        {
            new SingleAttackIntent(SlapDamage)
        });
        var seek = new MoveState("SEEK_MOVE", SeekMove, new AbstractIntent[]
        {
            new DefendIntent(),
            new BuffIntent()
        });
        var smash = new MoveState("SMASH_MOVE", SmashMove, new AbstractIntent[]
        {
            new SingleAttackIntent(SmashDamage),
            new DebuffIntent(true)
        });

        slap.FollowUpState = seek;
        seek.FollowUpState = smash;
        smash.FollowUpState = slap;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { slap, seek, smash },
            slap);
    }

    private async Task SlapMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SlapDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task SeekMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await CreatureCmd.GainBlock(
            Creature,
            GuardBlock,
            ValueProp.Unpowered,
            null,
            false);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    private async Task SmashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SmashDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await PowerCmd.Apply<FrailPower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            1m,
            Creature,
            null,
            false);
    }
}
