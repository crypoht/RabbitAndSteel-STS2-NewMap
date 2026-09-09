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
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using RabbitAndSteelNewMap.Scripts.Power;
using MegaCrit.Sts2.Core.Models.Powers;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Orn : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("ORN.name");

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 64, 61);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 67, 65);

    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/Orn.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(
            controller,
            idleName: "idle_loop",
            deadName: "die",
            hitName: "hurt",
            attackName: "attack",
            castName: "power",
            relaxedName: "idle_loop");

    private int ComboDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    private int HeavyDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 15);

    private int GuardBlock => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 11, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var combo = new MoveState("COMBO_MOVE", ComboMove, new SingleAttackIntent(ComboDamage));
        var heavy = new MoveState("HEAVY_MOVE", HeavyMove, new SingleAttackIntent(HeavyDamage));
        var guard = new MoveState("GUARD_MOVE", GuardMove, new DefendIntent());
        var weaken = new MoveState("WEAKEN_MOVE", WeakenMove, new DebuffIntent());
        var random = new RandomBranchState("RANDOM_A");

        random.AddBranch(combo, MoveRepeatType.CanRepeatForever);
        random.AddBranch(heavy, MoveRepeatType.CanRepeatForever);
        random.AddBranch(guard, MoveRepeatType.CanRepeatForever);
        random.AddBranch(weaken, MoveRepeatType.CanRepeatForever);

        combo.FollowUpState = random;
        heavy.FollowUpState = random;
        guard.FollowUpState = random;
        weaken.FollowUpState = random;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { combo, heavy, guard, weaken, random },
            random);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<RandomAdaptationPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    private async Task ComboMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ComboDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task HeavyMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HeavyDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
    }

    private async Task GuardMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await CreatureCmd.GainBlock(Creature, GuardBlock, ValueProp.Unpowered, null, false);
    }

    private async Task WeakenMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<WeakPower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            1m,
            Creature,
            null,
            false);
    }
}
