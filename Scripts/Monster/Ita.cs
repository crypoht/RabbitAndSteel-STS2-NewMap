using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
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
using RabbitAndSteelNewMap.Scripts.Card;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Ita : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("ITA.name");
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 40, 37);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 43, 40);
    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/Ita.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(controller,
            idleName: "idle_loop", deadName: "die", hitName: "hurt",
            attackName: "attack", castName: "power", relaxedName: "idle_loop");

    private int Vigor => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 3, 2);
    private int StaffDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);
    private int Vulnerable => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 3, 2);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var pull = new MoveState("PULL_AWAY_MOVE", PullAwayMove, new DebuffIntent());
        var chant = new MoveState("CHANT_MOVE", ChantMove, new BuffIntent());
        var staff = new MoveState("STAFF_MOVE", StaffMove, new SingleAttackIntent(StaffDamage));
        var distract = new MoveState("DISTRACT_MOVE", DistractMove, new DebuffIntent());

        // The encounter starts with the documented random-group fallback cycle.
        distract.FollowUpState = chant;
        chant.FollowUpState = pull;
        pull.FollowUpState = distract;
        staff.FollowUpState = chant;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { pull, chant, staff, distract },
            distract);
    }

    private async Task PullAwayMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        foreach (var target in targets.Where(t => t.Player != null))
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                Creature.CombatState!.CreateCard<Away>(target.Player!),
                PileType.Hand,
                target.Player!,
                CardPilePosition.Bottom);
            await PowerCmd.Apply<PullAwayPower>(
                new ThrowingPlayerChoiceContext(), target, 1m, Creature, null, false);
        }
    }

    private async Task ChantMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies != null)
        {
            await PowerCmd.Apply<VigorPower>(
                new ThrowingPlayerChoiceContext(), allies, Vigor, Creature, null, false);
        }
    }

    private async Task StaffMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StaffDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
    }

    private async Task DistractMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<VulnerablePower>(
            new ThrowingPlayerChoiceContext(), targets, Vulnerable, Creature, null, false);
    }
}
