using System.Collections.Generic;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Nimi : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("NIMI.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 43);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 47, 45);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Nimi.tscn");

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

    private int CoverBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 15, 12);

    private int VolleyDamage => 3;

    private int VolleyHits =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int HeavyDamage => 20;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var cover = new MoveState(
            "COVER_MOVE",
            CoverMove,
            new DefendIntent());
        var charge = new MoveState(
            "CHARGE_MOVE",
            ChargeMove,
            new BuffIntent());
        var volley = new MoveState(
            "VOLLEY_MOVE",
            VolleyMove,
            new MultiAttackIntent(VolleyDamage, VolleyHits));
        var heavy = new MoveState(
            "HEAVY_ATTACK_MOVE",
            HeavyAttackMove,
            new SingleAttackIntent(HeavyDamage));

        var random = new RandomBranchState("RANDOM_A");
        random.AddBranch(cover, MoveRepeatType.CanRepeatForever);
        random.AddBranch(charge, MoveRepeatType.CanRepeatForever);

        volley.FollowUpState = random;
        cover.FollowUpState = heavy;
        charge.FollowUpState = heavy;
        heavy.FollowUpState = volley;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { cover, charge, volley, heavy, random },
            volley);
    }

    private async Task CoverMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies == null)
            return;

        foreach (var ally in allies)
            await CreatureCmd.GainBlock(
                ally,
                CoverBlock,
                ValueProp.Unpowered,
                null,
                false);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> targets)
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

    private async Task VolleyMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(VolleyDamage)
            .WithHitCount(VolleyHits)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task HeavyAttackMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(HeavyDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
}
