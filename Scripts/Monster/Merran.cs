using System.Collections.Generic;
using System.Linq;
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
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Merran : ModMonsterTemplate
{
    private MoveState? _reviveState;

    public override LocString Title => MonsterModel.L10NMonsterLookup("MERRAN.name");
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 120, 110);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 125, 114);
    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/Merran.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

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

    private int FlowDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 12);
    private int SlashDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);
    private const int SlashHits = 3;
    private int StormDamage => 20;
    private int BreakDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);
    private int HowlBlock => 8;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<HowlPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null, false);
        await PowerCmd.Apply<MerranRevivalPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
    }

    public void ScheduleRevive()
    {
        if (_reviveState != null)
            SetMoveImmediate(_reviveState, true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var flow = new MoveState("FLOW_MOVE", FlowMove, new DebuffIntent(), new SingleAttackIntent(FlowDamage));
        var slash = new MoveState("SLASH_MOVE", SlashMove, new MultiAttackIntent(SlashDamage, SlashHits));
        var storm = new MoveState("STORM_MOVE", StormMove, new SingleAttackIntent(StormDamage), new DebuffIntent());
        var breakMove = new MoveState("BREAK_MOVE", BreakMove, new SingleAttackIntent(BreakDamage), new DebuffIntent());
        var howl = new MoveState("HOWL_MOVE", HowlMove, new BuffIntent(), new DefendIntent());
        var revive = new MoveState("REVIVE_MOVE", ReviveMove, new BuffIntent()) { MustPerformOnceBeforeTransitioning = true };

        flow.FollowUpState = slash;
        slash.FollowUpState = storm;
        storm.FollowUpState = flow;
        breakMove.FollowUpState = howl;
        howl.FollowUpState = slash;
        revive.FollowUpState = flow;
        _reviveState = revive;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { flow, slash, storm, breakMove, howl, revive }, flow);
    }

    private async Task FlowMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<TurbulencePower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
        await DamageCmd.Attack(FlowDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);
    }

    private async Task SlashMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(SlashDamage).WithHitCount(SlashHits).OnlyPlayAnimOnce().FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null).WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null).Execute(null);

    private async Task StormMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StormDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }

    private async Task BreakMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BreakDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
        await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    }

    private async Task HowlMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<HowlPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        await CreatureCmd.GainBlock(Creature, HowlBlock, ValueProp.Unpowered, null, false);
    }

    private async Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState;
        if (combatState == null)
            return;

        var room = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;
        var oldNode = room?.GetCreatureNode(Creature);
        var oldPosition = oldNode?.GlobalPosition;
        if (room != null && oldNode != null)
        {
            room.RemoveCreatureNode(oldNode);
            oldNode.QueueFree();
        }

        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.RemoveCreature(Creature);
        combatState.RemoveCreature(Creature, true);
        var second = await CreatureCmd.Add(ModelDb.Monster<MerranBig>().ToMutable(), combatState, Creature.Side, Creature.SlotName);
        second.SetNodeVisible(false);
        var secondNode = room?.GetCreatureNode(second);
        if (oldPosition != null && secondNode != null)
            secondNode.GlobalPosition = oldPosition.Value;
        second.SetNodeVisible(true);
    }
}
