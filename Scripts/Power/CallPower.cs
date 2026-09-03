using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RabbitAndSteelNewMap.Scripts.Affliction;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class CallPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner is null
            || cardPlay.Card.IsDupe
            || cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Affliction is not IColorMarkAffliction mark)
        {
            return Task.CompletedTask;
        }

        var data = GetInternalData<Data>();
        if (data.HasTriggered)
            return Task.CompletedTask;

        data.ColorCounts.TryGetValue(mark.Color, out var count);
        data.ColorCounts[mark.Color] = count + 1;
        if (data.ColorCounts.Count < 4 && data.ColorCounts.Values.All(colorCount => colorCount < 3))
            return Task.CompletedTask;

        data.HasTriggered = true;
        foreach (var enemy in Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
                 ?? Enumerable.Empty<Creature>())
        {
            switch (enemy.Monster)
            {
                case Avy avy:
                    avy.ScheduleCallReward();
                    break;
                case AvyBig avyBig:
                    avyBig.ScheduleCallReward();
                    break;
            }
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner is not null && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    private sealed class Data
    {
        public bool HasTriggered;
        public Dictionary<ColorMarkType, int> ColorCounts { get; } = new();
    }
}
