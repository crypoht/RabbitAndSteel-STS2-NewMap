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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public abstract class TasshaBase : ModMonsterTemplate, IAttackSideIntentProvider
{
    private bool _summoned;
    private MoveState? _cycleAEntry;
    private RandomBranchState? _attackRandomOne;
    private RandomBranchState? _buffRandomOne;
    private RandomBranchState? _attackRandomTwo;
    private RandomBranchState? _buffRandomTwo;
    protected abstract string ScenePath { get; }
    protected virtual bool IsLeader => false;
    public override LocString Title => MonsterModel.L10NMonsterLookup("TASSHA.name");
    public override MonsterAssetProfile AssetProfile => new(ScenePath);
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(ScenePath);
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

    protected int DestructionDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 15);
    protected int ImpactDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);
    protected int RuinDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);
    protected int PreparationAmount => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 4, 3);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!IsLeader)
            FlipLeftSideVisual();

        if (IsLeader)
        {
            var players = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Player);
            if (players != null)
            {
                await PowerCmd.Apply<AttackSideFacingPower>(
                    new ThrowingPlayerChoiceContext(),
                    players,
                    1m,
                    Creature,
                    null,
                    false);

                foreach (var player in players)
                {
                    var facing = player.GetPower<AttackSideFacingPower>();
                    if (facing != null)
                        await facing.InitializeDefaultDirection();
                }
            }
        }

        await PowerCmd.Apply<PhantomPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        if (IsLeader)
        {
            await PowerCmd.Apply<HowlPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
            await PowerCmd.Apply<AttackSideRightPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        }
        else
        {
            if (Creature.SlotName?.Contains("_right_") == true)
                await PowerCmd.Apply<AttackSideRightPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
            else
                await PowerCmd.Apply<AttackSideLeftPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
            BeginSummonedPhase();
        }
    }

    private bool IsLeft => !IsLeader;

    private void FlipLeftSideVisual()
    {
        if (!Creature.SlotName?.Contains("_left_") == true)
            return;

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        var spine = creatureNode?.Visuals.GetNodeOrNull<Node2D>("%Visuals");
        if (spine != null)
            spine.Scale = new Vector2(Mathf.Abs(spine.Scale.X), spine.Scale.Y);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var preDestruction = new MoveState("DESTRUCTION_MOVE", DestructionMove, new SingleAttackIntent(DestructionDamage));
        var preRuin = new MoveState("RUIN_MOVE", RuinMove, new MultiAttackIntent(RuinDamage, 2));
        var summonOne = new MoveState("SUMMON_ONE_MOVE", SummonOneMove, new BuffIntent(), new SummonIntent());
        var summonTwo = new MoveState("SUMMON_TWO_MOVE", SummonTwoMove, new BuffIntent(), new SummonIntent());
        var prePrepare = new MoveState("PREPARE_MOVE", PrepareMove, new BuffIntent());
        var randomSummon = new RandomBranchState("RANDOM_SUMMON");
        randomSummon.AddBranch(summonOne, MoveRepeatType.CanRepeatForever);
        randomSummon.AddBranch(summonTwo, MoveRepeatType.CanRepeatForever);

        var slotOne = new ConditionalBranchState("PHANTOM_SLOT_ONE");
        var slotTwo = new ConditionalBranchState("PHANTOM_SLOT_TWO");
        var slotThree = new MoveState("PHANTOM_SLOT_THREE", RecoverMove, new BuffIntent());

        var attackOneTurbulence = new MoveState("ATTACK_ONE_TURBULENCE", TurbulenceMove, new DebuffIntent());
        var attackOneImpact = new MoveState("ATTACK_ONE_IMPACT", ImpactMove, new SingleAttackIntent(ImpactDamage));
        var attackOneRuin = new MoveState("ATTACK_ONE_RUIN", RuinMove, new MultiAttackIntent(RuinDamage, 2));
        var buffOnePrepare = new MoveState("BUFF_ONE_PREPARE", PrepareMove, new BuffIntent());
        var buffOneTurbulence = new MoveState("BUFF_ONE_TURBULENCE", TurbulenceMove, new DebuffIntent());
        var buffOneHowl = new MoveState("BUFF_ONE_HOWL", HowlMove, new BuffIntent());
        var attackTwoTurbulence = new MoveState("ATTACK_TWO_TURBULENCE", TurbulenceMove, new DebuffIntent());
        var attackTwoImpact = new MoveState("ATTACK_TWO_IMPACT", ImpactMove, new SingleAttackIntent(ImpactDamage));
        var attackTwoRuin = new MoveState("ATTACK_TWO_RUIN", RuinMove, new MultiAttackIntent(RuinDamage, 2));
        var buffTwoPrepare = new MoveState("BUFF_TWO_PREPARE", PrepareMove, new BuffIntent());
        var buffTwoTurbulence = new MoveState("BUFF_TWO_TURBULENCE", TurbulenceMove, new DebuffIntent());
        var buffTwoHowl = new MoveState("BUFF_TWO_HOWL", HowlMove, new BuffIntent());

        var attackRandomOne = new RandomBranchState("ATTACK_RANDOM_ONE");
        attackRandomOne.AddBranch(attackOneTurbulence, MoveRepeatType.CanRepeatForever);
        attackRandomOne.AddBranch(attackOneImpact, MoveRepeatType.CanRepeatForever);
        attackRandomOne.AddBranch(attackOneRuin, MoveRepeatType.CanRepeatForever);
        var buffRandomOne = new RandomBranchState("BUFF_RANDOM_ONE");
        buffRandomOne.AddBranch(buffOnePrepare, MoveRepeatType.CannotRepeat);
        buffRandomOne.AddBranch(buffOneTurbulence, MoveRepeatType.CannotRepeat);
        buffRandomOne.AddBranch(buffOneHowl, MoveRepeatType.CannotRepeat);
        var attackRandomTwo = new RandomBranchState("ATTACK_RANDOM_TWO");
        attackRandomTwo.AddBranch(attackTwoTurbulence, MoveRepeatType.CanRepeatForever);
        attackRandomTwo.AddBranch(attackTwoImpact, MoveRepeatType.CanRepeatForever);
        attackRandomTwo.AddBranch(attackTwoRuin, MoveRepeatType.CanRepeatForever);
        var buffRandomTwo = new RandomBranchState("BUFF_RANDOM_TWO");
        buffRandomTwo.AddBranch(buffTwoPrepare, MoveRepeatType.CannotRepeat);
        buffRandomTwo.AddBranch(buffTwoTurbulence, MoveRepeatType.CannotRepeat);
        buffRandomTwo.AddBranch(buffTwoHowl, MoveRepeatType.CannotRepeat);

        _attackRandomOne = attackRandomOne;
        _buffRandomOne = buffRandomOne;
        _attackRandomTwo = attackRandomTwo;
        _buffRandomTwo = buffRandomTwo;

        // After the summon cycle, facing the creature's own side enters loop
        // C directly; facing away enters loop B. Keep these branches
        // mutually exclusive so the fallback branch cannot preserve loop C.
        slotOne.AddState(buffRandomOne, IsFacingOwnSide);
        slotOne.AddState(attackRandomOne, () => !IsFacingOwnSide());
        slotTwo.AddState(buffRandomTwo, IsFacingOwnSide);
        slotTwo.AddState(attackRandomTwo, () => !IsFacingOwnSide());
        foreach (var move in new[] { attackOneTurbulence, attackOneImpact, attackOneRuin, buffOnePrepare, buffOneTurbulence, buffOneHowl })
            move.FollowUpState = slotTwo;
        foreach (var move in new[] { attackTwoTurbulence, attackTwoImpact, attackTwoRuin, buffTwoPrepare, buffTwoTurbulence, buffTwoHowl })
            move.FollowUpState = slotThree;
        slotThree.FollowUpState = IsLeader ? prePrepare : slotThree;

        if (IsLeader)
        {
            prePrepare.FollowUpState = preDestruction;
            preDestruction.FollowUpState = preRuin;
            preRuin.FollowUpState = randomSummon;
            summonOne.FollowUpState = slotOne;
            summonTwo.FollowUpState = slotOne;
            _cycleAEntry = prePrepare;
        }
        else
        {
            slotThree.FollowUpState = slotThree;
        }

        return new MonsterMoveStateMachine(new List<MonsterState>
        { prePrepare, preDestruction, preRuin, summonOne, summonTwo, slotOne, slotTwo, slotThree,
          attackOneTurbulence, attackOneImpact, attackOneRuin, buffOnePrepare, buffOneTurbulence, buffOneHowl,
          attackTwoTurbulence, attackTwoImpact, attackTwoRuin, buffTwoPrepare, buffTwoTurbulence, buffTwoHowl,
          randomSummon, attackRandomOne, buffRandomOne, attackRandomTwo, buffRandomTwo }, IsLeader ? prePrepare : slotOne);
    }

    public void RefreshAttackSideIntent(AttackSideFacingPower.Direction direction)
    {
        var monster = Creature.Monster;
        if (!_summoned || monster == null)
            return;
        var moveStateMachine = monster.MoveStateMachine;
        if (moveStateMachine == null)
            return;
        var current = monster.NextMove.Id;
        if (current.Contains("THREE") || current == "PHANTOM_SLOT_THREE")
        {
            var slotThree = moveStateMachine.States["PHANTOM_SLOT_THREE"] as MoveState;
            if (slotThree != null)
                monster.SetMoveImmediate(slotThree, true);
            return;
        }

        bool isBranch = current == "PHANTOM_SLOT_ONE" || current == "PHANTOM_SLOT_TWO";
        bool isAttack = current.StartsWith("ATTACK_", StringComparison.Ordinal);
        bool isBuff = current.StartsWith("BUFF_", StringComparison.Ordinal);
        bool shouldUseCycleC = IsFacingOwnSide(direction);

        // Keep an already-correct intent. Re-rolling every turn here can move
        // a phantom to the wrong random group when the player turns around.
        if (!isBranch && ((shouldUseCycleC && isBuff) || (!shouldUseCycleC && isAttack)))
            return;

        bool isSecondSlot = current.Contains("TWO") || current == "PHANTOM_SLOT_TWO";
        var randomState = isSecondSlot
            ? (shouldUseCycleC ? _buffRandomTwo : _attackRandomTwo)
            : (shouldUseCycleC ? _buffRandomOne : _attackRandomOne);
        if (randomState == null)
            return;

        // ConditionalBranchState is only a resolver and cannot be used as a
        // SetMoveImmediate target. Resolve it to an actual MoveState first.
        string nextId;
        try
        {
            nextId = randomState.GetNextState(Creature, monster.RunRng.MonsterAi);
        }
        catch (InvalidOperationException)
        {
            nextId = randomState.States.Count > 0
                ? randomState.States[0].stateId
                : string.Empty;
        }

        if (nextId.Length > 0 &&
            moveStateMachine.States.TryGetValue(nextId, out var nextState) &&
            nextState is MoveState nextMove)
        {
            monster.SetMoveImmediate(nextMove, true);
        }
    }

    private bool IsFacingOwnSide() => AttackSideFacingPower.IsPlayerFacingCreatureSide(Creature);

    private bool IsFacingOwnSide(AttackSideFacingPower.Direction direction)
    {
        if (Creature.HasPower<AttackSideLeftPower>())
            return direction == AttackSideFacingPower.Direction.Left;
        if (Creature.HasPower<AttackSideRightPower>())
            return direction == AttackSideFacingPower.Direction.Right;
        return false;
    }
    private void BeginSummonedPhase() => _summoned = true;

    private async Task TurbulenceMove(IReadOnlyList<Creature> targets) => await PowerCmd.Apply<TurbulencePower>(new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null, false);
    private async Task DestructionMove(IReadOnlyList<Creature> targets) => await DamageCmd.Attack(DestructionDamage).FromMonster(this).WithAttackerAnim("Attack", .3f, null).WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);
    private async Task ImpactMove(IReadOnlyList<Creature> targets) => await DamageCmd.Attack(ImpactDamage).FromMonster(this).WithAttackerAnim("Attack", .3f, null).WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);
    private async Task RuinMove(IReadOnlyList<Creature> targets) => await DamageCmd.Attack(RuinDamage).WithHitCount(2).OnlyPlayAnimOnce().FromMonster(this).WithAttackerAnim("Attack", .3f, null).WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_slash", null, null).Execute(null);
    private async Task PrepareMove(IReadOnlyList<Creature> targets) => await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)?.Where(c => c.IsAlive).ToList() ?? new List<Creature>(), PreparationAmount, Creature, null, false);
    private async Task HowlMove(IReadOnlyList<Creature> targets) => await PowerCmd.Apply<HowlPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
    private async Task RecoverMove(IReadOnlyList<Creature> targets)
    {
        foreach (var phantom in Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)?.Where(c => c != Creature && c.IsAlive && c.HasPower<PhantomPower>()).ToList() ?? new List<Creature>())
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(phantom);
            if (node != null) { NCombatRoom.Instance!.RemoveCreatureNode(node); node.QueueFree(); }
            CombatManager.Instance.RemoveCreature(phantom);
            Creature.CombatState!.RemoveCreature(phantom, true);
        }

        // Recovery ends the summoned phase even when it was performed from
        // loop B. The boss's next state is therefore the start of loop A.
        _summoned = false;
        if (IsLeader && Creature.Monster != null && _cycleAEntry != null)
            Creature.Monster.SetMoveImmediate(_cycleAEntry, true);
    }
    private async Task SummonOneMove(IReadOnlyList<Creature> targets)
    {
        BeginSummonedPhase();
        if (IsLeader && Creature.CombatState != null)
            await SummonPhantom("tassha_phantom_left_1");
    }

    private async Task SummonTwoMove(IReadOnlyList<Creature> targets)
    {
        BeginSummonedPhase();
        if (!IsLeader || Creature.CombatState == null)
            return;

        await SummonPhantom("tassha_phantom_left_1");
        await SummonPhantom("tassha_phantom_left_2");
        await SummonPhantom("tassha_phantom_right_1");
    }

    private async Task SummonPhantom(string slotName)
    {
        if (Creature.CombatState == null)
            return;

        var remainingHp = Creature.CurrentHp;
        var leaderMaxHp = Creature.MaxHp;
        await CreatureCmd.Add<TasshaPhantom>(Creature.CombatState, slotName);

        var phantom = Creature.CombatState.GetCreaturesOnSide(CombatSide.Enemy)
            ?.FirstOrDefault(c => c.SlotName == slotName);
        if (phantom != null)
        {
            // Phantoms copy both the leader's maximum HP and the leader's
            // current HP at the moment they are summoned.
            await CreatureCmd.SetMaxAndCurrentHp(phantom, leaderMaxHp);
            var targetHp = remainingHp > phantom.MaxHp ? phantom.MaxHp : remainingHp;
            if (phantom.CurrentHp != targetHp)
                await CreatureCmd.SetCurrentHp(phantom, targetHp);
        }
    }
}

public sealed class Tassha : TasshaBase
{
    protected override string ScenePath => "res://mod/Monster/Tassha.tscn";
    protected override bool IsLeader => true;
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 140, 130);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 144, 134);
}

public sealed class TasshaPhantom : TasshaBase
{
    protected override string ScenePath => "res://mod/Monster/Tassha.tscn";
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 140, 130);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 144, 134);
}
