using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class PhantomPower : ModPowerTemplate
{
    private static bool IsPropagating;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && props.IsPoweredAttack())
        {
            var data = GetInternalData<Data>();
            data.HpBeforeDamage = Owner.CurrentHp;
            var blocked = props.HasFlag(ValueProp.Unblockable)
                ? 0m
                : Math.Min(amount, Owner.Block);
            data.PendingUnblockedDamage = (int)Math.Max(amount - blocked, 0m);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || delta >= 0 || Owner.CurrentHp > 0)
            return;

        var data = GetInternalData<Data>();
        // Fatal damage skips AfterDamageReceived in the official damage flow.
        // Keep the same full unblocked amount used by the non-fatal path so a
        // dying phantom still shares this hit with the remaining group.
        var damageToPropagate = data.PendingUnblockedDamage;
        data.PendingUnblockedDamage = 0;
        data.HpBeforeDamage = 0;
        if (damageToPropagate > 0)
            await PropagateDamage(damageToPropagate, null);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || !props.IsPoweredAttack())
            return;

        GetInternalData<Data>().PendingUnblockedDamage = 0;
        await PropagateDamage(result.UnblockedDamage, choiceContext);
    }

    private async Task PropagateDamage(int damage, PlayerChoiceContext? choiceContext)
    {
        if (damage <= 0 || IsPropagating)
            return;

        IsPropagating = true;
        try
        {
            var allies = Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
                .Where(c => c != Owner && c.IsAlive && c.HasPower<PhantomPower>()).ToList();
            if (allies == null)
                return;

            var context = choiceContext ?? new ThrowingPlayerChoiceContext();
            foreach (var ally in allies)
                await CreatureCmd.Damage(
                    context,
                    ally,
                    damage,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    null!);
        }
        finally
        {
            IsPropagating = false;
        }
    }

    private sealed class Data
    {
        public int PendingUnblockedDamage;
        public decimal HpBeforeDamage;
    }
}
