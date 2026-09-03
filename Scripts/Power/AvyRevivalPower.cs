using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using RabbitAndSteelNewMap.Scripts.Monster;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class AvyRevivalPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Owner && creature.Monster is Avy avy)
        {
            GetInternalData<Data>().IsReviving = true;
            await CreatureCmd.TriggerAnim(creature, "Dead", 0f);
            await Cmd.Wait(deathAnimLength > 0f ? deathAnimLength : 0.7f, true);

            avy.ScheduleRevive();
        }
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Owner || !GetInternalData<Data>().IsReviving;

    public override bool ShouldStopCombatFromEnding() => true;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        creature != Owner;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    private sealed class Data
    {
        public bool IsReviving;
    }
}
