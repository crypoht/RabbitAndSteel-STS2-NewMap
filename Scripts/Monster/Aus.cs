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
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Aus : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("AUS.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 53, 51);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 58, 55);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Aus.tscn");

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

    private int HeavyAmount => 2;

    private int HeavyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 0);

    private int SlashDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 14);

    private int SpinDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var heavyIntents = new List<AbstractIntent> { new DebuffIntent() };
        if (HeavyDamage > 0)
            heavyIntents.Add(new SingleAttackIntent(HeavyDamage));

        var heavy = new MoveState(
            "HEAVY_MOVE",
            HeavyMove,
            heavyIntents.ToArray());
        var slash = new MoveState(
            "SLASH_MOVE",
            SlashMove,
            new SingleAttackIntent(SlashDamage));
        var spin = new MoveState(
            "SPIN_MOVE",
            SpinMove,
            new MultiAttackIntent(SpinDamage, 2));

        heavy.FollowUpState = slash;
        slash.FollowUpState = spin;
        spin.FollowUpState = heavy;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { heavy, slash, spin },
            heavy);
    }

    private async Task HeavyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<HeavyPower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            HeavyAmount,
            Creature,
            null,
            false);

        if (HeavyDamage > 0)
        {
            await DamageCmd.Attack(HeavyDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f, null)
                .WithAttackerFx(null, AttackSfx, null)
                .WithHitFx("vfx/vfx_attack_blunt", null, null)
                .Execute(null);
        }
    }

    private async Task SlashMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(SlashDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);

    private async Task SpinMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(SpinDamage)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
}
