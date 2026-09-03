using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;
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
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using RabbitAndSteelNewMap.Scripts.Power;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Maxi : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("MAXI.name");

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 28, 25);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 36, 33);

    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/maxi.tscn");

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

    private int BiteDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 10);

    private int PrepareDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState("BITE_MOVE", BiteMove, new AbstractIntent[]
        {
            new SingleAttackIntent(BiteDamage)
        });
        var prepare = new MoveState("PREPARE_MOVE", PrepareMove, new AbstractIntent[]
        {
            new SingleAttackIntent(PrepareDamage),
            new BuffIntent()
        });
        var hex = new MoveState("HEX_MOVE", HexMove, new AbstractIntent[]
        {
            new DebuffIntent(true)
        });

        hex.FollowUpState = bite;
        bite.FollowUpState = prepare;
        prepare.FollowUpState = bite;

        return new MonsterMoveStateMachine(new List<MonsterState> { hex, bite, prepare }, hex);
    }

    private async Task BiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BiteDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, this.AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task PrepareMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PrepareDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, this.AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null, false);
    }

    private async Task HexMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.3f);
        if (base.Creature.GetPower<MaxiColorPower>() == null)
        {
            await PowerCmd.Apply<MaxiColorPower>(
                new ThrowingPlayerChoiceContext(),
                base.Creature,
                1m,
                base.Creature,
                null,
                false);
        }
    }
}
