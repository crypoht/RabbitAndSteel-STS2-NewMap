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
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Combat;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public abstract class MouseMinionBase : ModMonsterTemplate
{
    protected abstract string MonsterKey { get; }
    protected abstract string VisualScenePath { get; }

    public override LocString Title => MonsterModel.L10NMonsterLookup($"{MonsterKey}.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 15, 13);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 17, 15);

    public override MonsterAssetProfile AssetProfile => new(VisualScenePath);

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
        await PowerCmd.Apply<RandomAdaptationPower>(
            new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        await PowerCmd.Apply<MinionPower>(
            new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var attack = new MoveState(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(AttackDamage));
        var guard = new MoveState(
            "GUARD_MOVE",
            GuardMove,
            new DefendIntent());
        var weaken = new MoveState(
            "WEAKEN_MOVE",
            WeakenMove,
            new DebuffIntent());
        var vulnerable = new MoveState(
            "VULNERABLE_MOVE",
            VulnerableMove,
            new DebuffIntent());

        var random = new RandomBranchState("RANDOM_A");
        random.AddBranch(attack, MoveRepeatType.CanRepeatForever);
        random.AddBranch(guard, MoveRepeatType.CanRepeatForever);
        random.AddBranch(weaken, MoveRepeatType.CanRepeatForever);
        random.AddBranch(vulnerable, MoveRepeatType.CanRepeatForever);

        attack.FollowUpState = random;
        guard.FollowUpState = random;
        weaken.FollowUpState = random;
        vulnerable.FollowUpState = random;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { attack, guard, weaken, vulnerable, random },
            random);
    }

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int GuardAmount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 3, 2);

    private async Task AttackMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(AttackDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task GuardMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies is null)
            return;

        foreach (var ally in allies)
        {
            if (ally.IsAlive)
                await CreatureCmd.GainBlock(ally, GuardAmount, ValueProp.Unpowered, null, false);
        }
    }

    private async Task WeakenMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<WeakPower>(
            new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }

    private async Task VulnerableMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<VulnerablePower>(
            new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }
}
