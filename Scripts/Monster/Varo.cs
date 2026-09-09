using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Varo : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("VARO.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 69, 66);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 71, 68);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Meg.tscn");

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

    private int DestructionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);

    private int ChainDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private int CoverBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 14, 12);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var heavy = new MoveState("HEAVY_MOVE", HeavyMove, new DebuffIntent());
        var destruction = new MoveState(
            "DESTRUCTION_MOVE",
            DestructionMove,
            new SingleAttackIntent(DestructionDamage));
        var chain = new MoveState(
            "CHAIN_MOVE",
            ChainMove,
            new MultiAttackIntent(ChainDamage, 2));
        var cover = new MoveState("COVER_MOVE", CoverMove, new DefendIntent());

        heavy.FollowUpState = destruction;
        destruction.FollowUpState = chain;
        chain.FollowUpState = cover;
        cover.FollowUpState = heavy;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { heavy, destruction, chain, cover },
            heavy);
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

    private async Task HeavyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await PowerCmd.Apply<HeavyPower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            1m,
            Creature,
            null,
            false);
    }

    private async Task DestructionMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(DestructionDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task ChainMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(ChainDamage)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task CoverMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies is null)
            return;

        foreach (var ally in allies)
            await CreatureCmd.GainBlock(
                ally,
                CoverBlock,
                ValueProp.Unpowered,
                null,
                false);
    }
}
