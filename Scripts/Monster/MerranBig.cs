using System;
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
using MegaCrit.Sts2.Core.Factories;
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
using RabbitAndSteelNewMap.Scripts.Power;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.MonsterMoves;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class MerranBig : ModMonsterTemplate
{
    private int _cycleCount;
    private bool IsPhantom => Creature.SlotName?.Contains("merran_phantom", StringComparison.OrdinalIgnoreCase) == true;

    public override LocString Title => MonsterModel.L10NMonsterLookup("MERRAN_BIG.name");
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 180);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 212, 190);
    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/MerranBig.tscn");

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

    private int KillDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);
    private int FinalDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1500, 150);
    private int WindDamage => 6;
    private const int WindHits = 3;
    private int HowlDamage => 8;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (IsPhantom)
        {
            await PowerCmd.Apply<MerranPhantomPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
            ApplyPhantomTint();
            return;
        }

        await PowerCmd.Apply<HowlPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null, false);
        await PowerCmd.Apply<MerranWindPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        await PowerCmd.Apply<MerranPhantomPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        var players = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Player);
        if (players == null)
            return;

        // Keep one hidden baseline layer so the 0-layer power remains present.
        await PowerCmd.Apply<MerranAttentionPower>(
            new ThrowingPlayerChoiceContext(),
            players,
            1m,
            Creature,
            null,
            false);
        await PowerCmd.Apply<MerranAttackCostPower>(
            new ThrowingPlayerChoiceContext(),
            players,
            1m,
            Creature,
            null,
            false);
        foreach (var player in players)
        {
            if (player.Player?.RunState.Rng.CombatTargets.NextBool() == true)
            {
                await PowerCmd.Apply<MerranAttackSideLeftPower>(
                    new ThrowingPlayerChoiceContext(), player, 1m, Creature, null, false);
            }
            else
            {
                await PowerCmd.Apply<MerranAttackSideRightPower>(
                    new ThrowingPlayerChoiceContext(), player, 1m, Creature, null, false);
            }
        }

        await SummonPhantom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var kill = new MoveState("KILL_MOVE", KillMove, new SingleAttackIntent(KillDamage));
        var notice = new MoveState("NOTICE_MOVE", NoticeMove, new DebuffIntent());
        var finale = new MoveState("FINALE_MOVE", FinaleMove, new SingleAttackIntent(FinalDamage));
        var wind = new MoveState("WIND_MOVE", WindMove, new MultiAttackIntent(WindDamage, WindHits));
        var howl = new MoveState("HOWL_MOVE", HowlMove, new SingleAttackIntent(HowlDamage), new BuffIntent());
        var cycle = new ConditionalBranchState("CYCLE_BRANCH");
        cycle.AddState(finale, () => _cycleCount >= 10);
        cycle.AddState(kill, () => true);

        if (IsPhantom)
        {
            kill.FollowUpState = wind;
            wind.FollowUpState = howl;
            howl.FollowUpState = kill;
            return new MonsterMoveStateMachine(new List<MonsterState> { kill, wind, howl }, kill);
        }

        notice.FollowUpState = kill;
        kill.FollowUpState = wind;
        wind.FollowUpState = howl;
        howl.FollowUpState = cycle;
        return new MonsterMoveStateMachine(new List<MonsterState> { kill, notice, finale, wind, howl, cycle }, notice);
    }

    private async Task KillMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(KillDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);

    private async Task NoticeMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        foreach (var player in Creature.CombatState?.Players ?? Enumerable.Empty<MegaCrit.Sts2.Core.Entities.Players.Player>())
        {
            var piles = new[] { PileType.Hand, PileType.Exhaust, PileType.Draw, PileType.Discard };
            var cards = piles.SelectMany(pile => pile.GetPile(player).Cards)
                .Where(card => card.Type != CardType.Attack)
                .Distinct()
                .ToList();
            if (cards.Count == 0)
                continue;

            var count = cards.Count;
            await CardPileCmd.RemoveFromCombat(cards, false);
            var generated = CardFactory.GetForCombat(
                player,
                player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                    .Where(card => card.Type == CardType.Attack),
                count,
                player.RunState.Rng.CombatCardGeneration);
            foreach (var card in generated)
            {
                card.SetToFreeThisCombat();
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player, CardPilePosition.Random);
            }
        }
    }

    private async Task FinaleMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(FinalDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);

    private async Task WindMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(WindDamage).WithHitCount(WindHits).OnlyPlayAnimOnce().FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null).WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_slash", null, null).Execute(null);

    private async Task HowlMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HowlDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null).WithHitFx("vfx/vfx_attack_blunt", null, null).Execute(null);
        await PowerCmd.Apply<HowlPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null, false);
        _cycleCount++;
    }

    private async Task SummonPhantom()
    {
        var state = Creature.CombatState;
        if (state == null || state.Enemies.Any(enemy => enemy.SlotName == "merran_phantom" && enemy.IsAlive))
            return;

        await CreatureCmd.Add<MerranBig>(state, "merran_phantom");
        var phantom = state.Enemies.FirstOrDefault(enemy => enemy.SlotName == "merran_phantom");
        if (phantom == null)
            return;

        await CreatureCmd.SetMaxAndCurrentHp(phantom, Creature.MaxHp);
        await CreatureCmd.SetCurrentHp(phantom, Creature.CurrentHp);
        ApplyPhantomTint(phantom);
    }

    private void ApplyPhantomTint() => ApplyPhantomTint(Creature);

    private static void ApplyPhantomTint(Creature phantom)
    {
        var node = NCombatRoom.Instance?.GetCreatureNode(phantom);
        if (node?.Visuals != null)
        {
            // Flip the actual Spine node; flipping the NCreatureVisuals root is
            // overwritten by the combat layout on some summoned enemies.
            var spine = node.Visuals.GetNodeOrNull<Node2D>("%Visuals");
            if (spine != null)
                spine.Scale = new Vector2(Math.Abs(spine.Scale.X), spine.Scale.Y);

            node.Visuals.Modulate = new Color(0.55f, 0.75f, 1f, 1f);
        }
    }
}
