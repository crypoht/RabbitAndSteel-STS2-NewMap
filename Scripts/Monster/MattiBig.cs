using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
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
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class MattiBig : ModMonsterTemplate
{
    private int _groupLoopCount;
    private bool _isAtHighPosition;

    public override LocString Title => MonsterModel.L10NMonsterLookup("MATTI_BIG.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 134, 130);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 136, 132);

    public override MonsterAssetProfile AssetProfile =>
        new("res://mod/Monster/Matti_big.tscn");

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

    private int ImpactDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private int SuppressionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 18, 17);

    private int VigorAmount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 3, 2);

    private int GroupBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 10, 8);

    private const int FinaleDamage = 110;
    private const int UnityAmount = 8;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await CreatureCmd.SetMaxAndCurrentHp(Creature, Creature.MaxHp);
        await PowerCmd.Apply<UnityPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            UnityAmount,
            Creature,
            null,
            false);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var impact = new MoveState(
            "IMPACT_MOVE",
            ImpactMove,
            new MultiAttackIntent(ImpactDamage, 2));
        var suppression = new MoveState(
            "SUPPRESSION_MOVE",
            SuppressionMove,
            new SingleAttackIntent(SuppressionDamage),
            new DebuffIntent());
        var summon = new MoveState(
            "SUMMON_MOVE",
            SummonMove,
            new SummonIntent());
        var vigor = new MoveState(
            "VIGOR_MOVE",
            VigorMove,
            new BuffIntent());
        var group = new MoveState(
            "GROUP_MOVE",
            GroupMove,
            new DefendIntent());
        var finale = new MoveState(
            "FINALE_MOVE",
            FinaleMove,
            new SingleAttackIntent(FinaleDamage));
        var empty = new MoveState("EMPTY_MOVE", EmptyMove);

        var afterGroup = new ConditionalBranchState("AFTER_GROUP_BRANCH");
        afterGroup.AddState(finale, () => _groupLoopCount >= 4);
        afterGroup.AddState(vigor, () => _groupLoopCount < 4);

        var afterFinale = new ConditionalBranchState("AFTER_FINALE_BRANCH");
        afterFinale.AddState(empty, IsOnlyLivingEnemy);
        afterFinale.AddState(impact, () => !IsOnlyLivingEnemy());

        impact.FollowUpState = suppression;
        suppression.FollowUpState = summon;
        summon.FollowUpState = vigor;
        vigor.FollowUpState = group;
        group.FollowUpState = afterGroup;
        finale.FollowUpState = afterFinale;
        empty.FollowUpState = empty;

        return new MonsterMoveStateMachine(
            new List<MonsterState>
            {
                impact, suppression, summon, vigor, group, finale, empty,
                afterGroup, afterFinale
            },
            impact);
    }

    private async Task ImpactMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(ImpactDamage)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task SuppressionMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SuppressionDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            targets,
            -1m,
            Creature,
            null,
            false);
    }

    private async Task SummonMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await MoveToHighPosition();

        var combatState = Creature.CombatState;
        if (combatState is null)
            return;

        await AddMinion<AusSmall>("aus_small");
        await AddMinion<ItaSmall>("ita_small");
        await AddMinion<MegSmall>("meg_small");
        await AddMinion<NimiSmall>("nimi_small");
        await AddMinion<OrnSmall>("orn_small");
        await AddMinion<PineSmall>("pine_small");
        await AddMinion<VaroSmall>("varo_small");
    }

    private async Task AddMinion<T>(string slot)
        where T : ModMonsterTemplate
    {
        var combatState = Creature.CombatState;
        if (combatState is null ||
            combatState.Enemies.Any(enemy => enemy.SlotName == slot && enemy.IsAlive))
        {
            return;
        }

        await CreatureCmd.Add<T>(combatState, slot);
    }

    private async Task VigorMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var enemies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies is not null)
        {
            await PowerCmd.Apply<VigorPower>(
                new ThrowingPlayerChoiceContext(),
                enemies.Where(enemy => enemy.IsAlive).ToList(),
                VigorAmount,
                Creature,
                null,
                false);
        }
    }

    private async Task GroupMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var enemies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies is not null)
        {
            foreach (var enemy in enemies.Where(enemy => enemy.IsAlive))
                await CreatureCmd.GainBlock(
                    enemy,
                    GroupBlock,
                    ValueProp.Unpowered,
                    null,
                    false);
        }

        _groupLoopCount++;
    }

    private async Task FinaleMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(FinaleDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task EmptyMove(IReadOnlyList<Creature> targets)
    {
        await MoveToOriginalPosition();
    }

    private async Task MoveToHighPosition()
    {
        if (_isAtHighPosition)
            return;

        await MoveToEncounterMarker("matti_high");
        _isAtHighPosition = true;
    }

    private async Task MoveToOriginalPosition()
    {
        if (!_isAtHighPosition)
            return;

        await MoveToEncounterMarker("matti");
        _isAtHighPosition = false;
    }

    private async Task MoveToEncounterMarker(string markerName)
    {
        var combatRoom = NCombatRoom.Instance;
        var creatureNode = combatRoom?.GetCreatureNode(Creature);
        var marker = combatRoom?.FindChild(markerName, true, false) as Marker2D;
        if (creatureNode is null || marker is null)
            return;

        var tween = creatureNode.CreateTween();
        tween.TweenProperty(
                creatureNode,
                "global_position",
                marker.GlobalPosition,
                0.6f)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Cubic);
        await tween.AwaitFinished(creatureNode);
    }

    private bool IsOnlyLivingEnemy() =>
        Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
            .Count(enemy => enemy.IsAlive) <= 1;
}
