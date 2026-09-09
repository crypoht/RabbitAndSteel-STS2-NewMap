using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
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
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Card;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Matti : ModMonsterTemplate
{
    private MoveState? _reviveState;

    public override LocString Title => MonsterModel.L10NMonsterLookup("MATTI.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 120, 110);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 124, 112);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Matti.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(
            controller,
            idleName: "idle_loop",
            deadName: "die",
            deadLoop: true,
            hitName: "hurt",
            attackName: "attack",
            castName: "power",
            relaxedName: "idle_loop");

    private int HeavyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    private int LineBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 17, 15);

    private int DestructionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 23);

    private int InspirationStrength =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 2, 1);

    private int ImpactDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int ShieldThrustDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MattiRevivalPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null,
            false);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var heavy = new MoveState(
            "HEAVY_MOVE",
            HeavyMove,
            new DebuffIntent(),
            new SingleAttackIntent(HeavyDamage));
        var line = new MoveState(
            "LINE_MOVE",
            LineMove,
            new DebuffIntent(),
            new DefendIntent());
        var destruction = new MoveState(
            "DESTRUCTION_MOVE",
            DestructionMove,
            new SingleAttackIntent(DestructionDamage));
        var inspiration = new MoveState(
            "INSPIRATION_MOVE",
            InspirationMove,
            new BuffIntent());
        var impact = new MoveState(
            "IMPACT_MOVE",
            ImpactMove,
            new MultiAttackIntent(ImpactDamage, 5));
        var shieldThrust = new MoveState(
            "SHIELD_THRUST_MOVE",
            ShieldThrustMove,
            new SingleAttackIntent(ShieldThrustDamage),
            new DefendIntent());
        var revive = new MoveState(
            "REVIVE_MOVE",
            ReviveMove,
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        heavy.FollowUpState = shieldThrust;
        shieldThrust.FollowUpState = line;
        line.FollowUpState = destruction;
        destruction.FollowUpState = inspiration;
        inspiration.FollowUpState = impact;
        impact.FollowUpState = heavy;
        revive.FollowUpState = heavy;
        _reviveState = revive;

        return new MonsterMoveStateMachine(
            new List<MonsterState>
            {
                heavy, line, destruction, inspiration, impact, shieldThrust, revive
            },
            heavy);
    }

    public void ScheduleRevive()
    {
        if (_reviveState is not null)
            SetMoveImmediate(_reviveState, true);
    }

    private async Task HeavyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<HeavyPower>(
            new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
        await DamageCmd.Attack(HeavyDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
    }

    private async Task LineMove(IReadOnlyList<Creature> targets)
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

        await CreatureCmd.GainBlock(
            Creature, LineBlock, ValueProp.Unpowered, null, false);
    }

    private async Task DestructionMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(DestructionDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task InspirationMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            InspirationStrength,
            Creature,
            null,
            false);
    }

    private async Task ImpactMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(ImpactDamage)
            .WithHitCount(5)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task ShieldThrustMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ShieldThrustDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await CreatureCmd.GainBlock(
            Creature, ShieldThrustDamage, ValueProp.Unpowered, null, false);
    }

    private async Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState;
        if (combatState is null)
            return;

        var combatRoom = NCombatRoom.Instance;
        var oldNode = combatRoom?.GetCreatureNode(Creature);
        var oldPosition = oldNode?.GlobalPosition;
        if (combatRoom is not null && oldNode is not null)
        {
            combatRoom.RemoveCreatureNode(oldNode);
            oldNode.QueueFree();
        }

        CombatManager.Instance.RemoveCreature(Creature);
        combatState.RemoveCreature(Creature, true);

        var secondPhase = await CreatureCmd.Add(
            ModelDb.Monster<MattiBig>().ToMutable(),
            combatState,
            Creature.Side,
            Creature.SlotName);

        secondPhase.SetNodeVisible(false);
        var secondNode = combatRoom?.GetCreatureNode(secondPhase);
        if (oldPosition is not null && secondNode is not null)
            secondNode.GlobalPosition = oldPosition.Value;
        secondPhase.SetNodeVisible(true);
    }
}
