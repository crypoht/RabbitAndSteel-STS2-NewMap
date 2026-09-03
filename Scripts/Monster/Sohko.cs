using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
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
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Sohko : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("SOHKO.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 39, 35);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 40);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Sohko.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            AssetProfile.VisualsScenePath!);
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

    private int RotationDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 13);

    private int ScatterDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private int PushCardCount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int MoveBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 11, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var push = new MoveState("PUSH_MOVE", PushMove, new AbstractIntent[]
        {
            new DebuffIntent()
        });
        var rotation = new MoveState("ROTATION_MOVE", RotationMove, new AbstractIntent[]
        {
            new SingleAttackIntent(RotationDamage)
        });
        var scatter = new MoveState("SCATTER_MOVE", ScatterMove, new AbstractIntent[]
        {
            new MultiAttackIntent(ScatterDamage, 2)
        });
        var move = new MoveState("MOVE_MOVE", MoveMove, new AbstractIntent[]
        {
            new DefendIntent(),
            new BuffIntent()
        });

        var random = new RandomBranchState("RANDOM_A");
        random.AddBranch(rotation, MoveRepeatType.CanRepeatForever);
        random.AddBranch(scatter, MoveRepeatType.CanRepeatForever);
        random.AddBranch(move, MoveRepeatType.CanRepeatForever);

        // The documented cycle is random A -> action 1.
        push.FollowUpState = random;
        rotation.FollowUpState = push;
        scatter.FollowUpState = push;
        move.FollowUpState = push;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { push, rotation, scatter, move, random },
            scatter);
    }

    private async Task PushMove(IReadOnlyList<Creature> targets)
    {
        var target = targets.FirstOrDefault(creature => creature.Player != null);
        if (target?.Player != null)
        {
            var discardPile = PileType.Discard.GetPile(target.Player);
            var cardsToPush = discardPile.Cards
                .Take(PushCardCount)
                .ToList();

            if (cardsToPush.Count > 0)
            {
                await CardPileCmd.Add(
                    cardsToPush,
                    PileType.Draw,
                    CardPilePosition.Random,
                    null,
                    false);
            }
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
    }

    private async Task RotationMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(RotationDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task ScatterMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ScatterDamage)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(null);
    }

    private async Task MoveMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await CreatureCmd.GainBlock(
            Creature,
            MoveBlock,
            ValueProp.Unpowered,
            null,
            false);
        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            2m,
            Creature,
            null,
            false);
    }
}
