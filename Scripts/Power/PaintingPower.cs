using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RabbitAndSteelNewMap.Scripts.Affliction;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class PaintingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner == null
            || cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Affliction is not IColorMarkAffliction mark)
        {
            return;
        }

        var data = GetInternalData<Data>();
        if (data.HasTriggered)
            return;

        data.PlayedColors.Add(mark.Color);
        if (data.PlayedColors.Count < 3)
            return;

        data.HasTriggered = true;
        var enemies = Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy);
        if (enemies == null)
            return;

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            enemies,
            1m,
            Applier,
            null,
            false);

        foreach (var enemy in enemies)
        {
            if (enemy.GetPower<VigorPower>() != null)
                await PowerCmd.Remove<VigorPower>(enemy);

            if (enemy.Monster is Blush blush)
                blush.ScheduleFatigue();
        }
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Owner == null)
            return;

        if (participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    private sealed class Data
    {
        public bool HasTriggered;
        public HashSet<ColorMarkType> PlayedColors { get; } = new();
    }
}
