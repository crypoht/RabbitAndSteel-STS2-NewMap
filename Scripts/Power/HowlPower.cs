using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public sealed class HowlPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        // Match the official SpeedsterPower convention: start-of-turn draws
        // use fromHandDraw=true, while extra draws use false.
        if (fromHandDraw)
            return;

        var playerCreature = card.Owner?.Creature;
        if (playerCreature == null ||
            playerCreature.Side != CombatSide.Player ||
            playerCreature.CombatState?.CurrentSide != CombatSide.Player ||
            Amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<FearPower>(
            choiceContext,
            playerCreature,
            Amount,
            Owner,
            null,
            false);
    }
}
