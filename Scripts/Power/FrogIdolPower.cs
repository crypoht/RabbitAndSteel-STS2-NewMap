using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class FrogIdolPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner is null || !participants.Contains(Owner))
            return;

        var data = GetInternalData<Data>();
        data.EnemyTurnStarts++;
        if (data.EnemyTurnStarts % 2 != 0)
            return;

        foreach (var target in combatState.PlayerCreatures.Where(target => target.Player != null))
            await ConstrainedPower.ApplyToPlayer(target.Player!, Owner);
    }

    private sealed class Data
    {
        public int EnemyTurnStarts;
    }
}
