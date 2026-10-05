using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class MerranPhantomPower : ModPowerTemplate
{
    private static bool _propagating;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (IsPhantom(Owner) && dealer == Owner && props.IsPoweredAttack())
            return 0.5m;
        return 1m;
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (IsPhantom(Owner) && target == Owner && props.IsPoweredAttack())
            GetInternalData<Data>().PendingDamage = (int)decimal.Max(0m, amount - Owner.Block);
        return Task.CompletedTask;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (!IsPhantom(Owner) || creature != Owner || delta >= 0 || Owner.CurrentHp > 0)
            return;

        var damage = GetInternalData<Data>().PendingDamage;
        GetInternalData<Data>().PendingDamage = 0;
        await Propagate(damage, null);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!IsPhantom(Owner) || target != Owner || result.UnblockedDamage <= 0 || !props.IsPoweredAttack())
            return;

        GetInternalData<Data>().PendingDamage = 0;
        await Propagate(result.UnblockedDamage, choiceContext);
    }

    private async Task Propagate(int damage, PlayerChoiceContext? choiceContext)
    {
        if (damage <= 0 || _propagating)
            return;

        _propagating = true;
        try
        {
            var real = Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
                .FirstOrDefault(c => c != Owner && c.IsAlive && !c.HasPower<MerranPhantomPower>());
            if (real != null)
            {
                await CreatureCmd.Damage(
                    choiceContext ?? new ThrowingPlayerChoiceContext(),
                    real,
                    damage,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    null!);
            }
        }
        finally
        {
            _propagating = false;
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Owner || IsPhantom(Owner))
            return;

        var phantoms = Owner.CombatState?.GetCreaturesOnSide(CombatSide.Enemy)
            .Where(c => c.IsAlive && IsPhantom(c))
            .ToList();
        if (phantoms is { Count: > 0 })
            await CreatureCmd.Kill(phantoms, true);
    }

    private static bool IsPhantom(Creature creature) =>
        creature.SlotName?.Contains("merran_phantom", System.StringComparison.OrdinalIgnoreCase) == true;

    private sealed class Data
    {
        public int PendingDamage;
    }
}
