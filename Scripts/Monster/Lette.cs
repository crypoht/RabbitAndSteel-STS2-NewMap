using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using RabbitAndSteelNewMap.Scripts.Power;

namespace RabbitAndSteelNewMap.Scripts.Monster;

public sealed class Lette : ModMonsterTemplate
{
    public override LocString Title => MonsterModel.L10NMonsterLookup("LETTE.name");
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 41, 44);
    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 47);
    public override MonsterAssetProfile AssetProfile => new("res://mod/Monster/Lette.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    protected override CreatureAnimator? SetupCustomCreatureAnimator(MegaSprite controller) =>
        ModAnimStateMachines.Standard(controller,
            idleName: "idle_loop", deadName: "die", hitName: "hurt",
            attackName: "attack", castName: "power", relaxedName: "idle_loop");

    private int VoiceDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);
    private int BreathBlock => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 13, 11);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var control = new MoveState("CONTROL_MOVE", ControlMove, new DebuffIntent());
        var soothe = new MoveState("SOOTHE_MOVE", SootheMove, new BuffIntent());
        var voice = new MoveState("VOICE_MOVE", VoiceMove, new MultiAttackIntent(VoiceDamage, 3));
        var breath = new MoveState("BREATH_MOVE", BreathMove, new DefendIntent());

        control.FollowUpState = soothe;
        soothe.FollowUpState = voice;
        voice.FollowUpState = breath;
        breath.FollowUpState = control;

        return new MonsterMoveStateMachine(
            new List<MonsterState> { control, soothe, voice, breath },
            control);
    }

    private async Task ControlMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        foreach (var target in targets.Where(t => t.Player != null))
            await ApplyBoundCards(target.Player!);
    }

    private async Task ApplyBoundCards(Player player)
    {
        await ConstrainedPower.ApplyToPlayer(player, Creature);
    }

    private async Task SootheMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var allies = Creature.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (allies != null)
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), allies, 2m, Creature, null, false);
    }

    private async Task VoiceMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(VoiceDamage)
            .WithHitCount(3)
            .OnlyPlayAnimOnce()
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f, null)
            .WithAttackerFx(null, AttackSfx, null)
            .WithHitFx("vfx/vfx_attack_blunt", null, null)
            .Execute(null);

    private async Task BreathMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await CreatureCmd.GainBlock(Creature, BreathBlock, ValueProp.Unpowered, null, false);
    }
}
