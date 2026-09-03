using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Pine : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("PINE.name");
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 43, 41);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 46, 43);
    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/Pine.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(controller,
            idleName: "idle_loop", deadName: "die", hitName: "hurt",
            attackName: "attack", castName: "power", relaxedName: "idle_loop");

    private int Block => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 10, 9);
    private int PokeDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 7);
    private int BreakDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 14);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var circle = new MoveState("CIRCLE_MOVE", CircleMove, new DefendIntent(), new BuffIntent());
        var poke = new MoveState("POKE_MOVE", PokeMove, new SingleAttackIntent(PokeDamage));
        var breakMove = new MoveState("BREAK_MOVE", BreakMove, new SingleAttackIntent(BreakDamage));
        var probe = new MoveState("PROBE_MOVE", ProbeMove, new DebuffIntent());

        probe.FollowUpState = poke;
        poke.FollowUpState = circle;
        circle.FollowUpState = breakMove;
        breakMove.FollowUpState = probe;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { circle, poke, breakMove, probe },
            probe);
    }

    private async Task CircleMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies != null)
        {
            foreach (var ally in allies)
                await CreatureCmd.GainBlock(ally, Block, ValueProp.Unpowered, null, false);
        }
    }

    private async Task PokeMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(PokeDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task BreakMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(BreakDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task ProbeMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
            targets, 1m, Creature, null, false);
    }
}
