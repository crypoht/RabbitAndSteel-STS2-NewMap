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
using RabbitAndSteelNewMap.Scripts.Card;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Meg : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("MEG.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 69, 66);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 71, 68);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Varo.tscn");

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

    private int SpellDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    private int HealingAmount(Creature creature) =>
        (int)decimal.Ceiling(creature.MaxHp * 0.1m);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var spell = new MoveState(
            "SPELL_MOVE",
            SpellMove,
            new SingleAttackIntent(SpellDamage),
            new DebuffIntent());
        var pullAway = new MoveState("PULL_AWAY_MOVE", PullAwayMove, new DebuffIntent());
        var empower = new MoveState("EMPOWER_MOVE", EmpowerMove, new BuffIntent());
        var heal = new MoveState("HEAL_MOVE", HealMove, new HealIntent());

        spell.FollowUpState = pullAway;
        pullAway.FollowUpState = empower;
        empower.FollowUpState = heal;
        heal.FollowUpState = spell;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { spell, pullAway, empower, heal },
            spell);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<BalancePower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    private async Task SpellMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SpellDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

        await PowerCmd.Apply<VulnerablePower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            1m,
            Creature,
            null,
            false);
    }

    private async Task PullAwayMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        foreach (var target in targets.Where(target => target.Player != null))
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                Creature.CombatState!.CreateCard<Away>(target.Player!),
                PileType.Hand,
                target.Player!,
                CardPilePosition.Bottom);
        }
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies != null)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                allies,
                AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 2, 1),
                Creature,
                null,
                false);
        }
    }

    private async Task HealMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies is null)
            return;

        foreach (var ally in allies.Where(ally => ally.IsAlive))
            await CreatureCmd.Heal(ally, HealingAmount(ally), true);
    }
}
