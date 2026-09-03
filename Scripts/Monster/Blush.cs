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
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using RabbitAndSteelNewMap.Scripts.Power;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Blush : ModMonsterTemplate
{
    private MoveState? _fatigueState;

    public override LocString Title => MonsterModel.L10NMonsterLookup("BLUSH.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 130, 121);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 134, 124);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Blush.tscn");

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

    private int DoodleDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private int MixVigor =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 6, 4);

    private int FatigueDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var paint = new MoveState("PAINT_MOVE", PaintMove, new BuffIntent());
        var doodle = new MoveState(
            "DOODLE_MOVE",
            DoodleMove,
            new MultiAttackIntent(DoodleDamage, 3));
        var mix = new MoveState("MIX_MOVE", MixMove, new BuffIntent());
        var contrast = new MoveState("CONTRAST_MOVE", ContrastMove, new DebuffIntent());
        var fatigue = new MoveState(
            "FATIGUE_MOVE",
            FatigueMove,
            new SingleAttackIntent(FatigueDamage));

        paint.FollowUpState = doodle;
        doodle.FollowUpState = mix;
        mix.FollowUpState = contrast;
        contrast.FollowUpState = doodle;
        fatigue.FollowUpState = doodle;

        _fatigueState = fatigue;
        return new MonsterMoveStateMachine(
            new List<MonsterState> { paint, doodle, mix, contrast, fatigue },
            paint);
    }

    public void ScheduleFatigue()
    {
        if (_fatigueState != null)
            SetMoveImmediate(_fatigueState);
    }

    private async Task PaintMove(IReadOnlyList<Creature> targets)
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

    private async Task DoodleMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(DoodleDamage)
            .WithHitCount(3)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task MixMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            MixVigor,
            Creature,
            null,
            false);
    }

    private async Task ContrastMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<PaintingPower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            1m,
            Creature,
            null,
            false);
    }

    private async Task FatigueMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(FatigueDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
}
