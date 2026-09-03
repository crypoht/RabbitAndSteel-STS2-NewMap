using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RabbitAndSteelNewMap.Scripts.Affliction;
using RabbitAndSteelNewMap.Scripts.Capability;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Power;

public abstract class ColorMechanicPower : ModPowerTemplate
{
    protected Creature ColorOwner =>
        Owner ?? throw new InvalidOperationException("Color mechanic power has no owner.");

    protected ICombatState ColorCombatState =>
        ColorOwner.CombatState ?? throw new InvalidOperationException("Color mechanic power has no combat state.");

    protected abstract IReadOnlyList<ColorMarkType> AvailableColors { get; }

    protected virtual int RedDamage => 7;

    protected virtual int BlueBlock => 10;

    protected virtual int YellowStrength => 2;

    protected virtual int PurpleVulnerable => 2;

    protected virtual int GreenWeak => 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var players = ColorCombatState.Players;
        var rngPlayer = players.FirstOrDefault();
        if (rngPlayer is null || !ColorCombatState.PlayerCreatures.Contains(player.Creature))
            return;

        var data = GetInternalData<Data>();
        if (data.AssignedRound != ColorCombatState.RoundNumber)
        {
            if (AvailableColors.Count == 0)
                return;

            data.RequiredColor = AvailableColors[rngPlayer.RunState.Rng.CombatTargets.NextInt(AvailableColors.Count)];
            data.AssignedRound = ColorCombatState.RoundNumber;
            data.LastColors.Clear();
        }

        await UpdateRequiredColorDisplay(choiceContext, data.RequiredColor);
        await AssignColorsToHand(choiceContext, player);
    }

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (GetInternalData<Data>().AssignedRound != ColorCombatState.RoundNumber
            || card.Pile?.Type != PileType.Hand
            || !CanReceiveColor(card))
            return Task.CompletedTask;

        if (card.Owner.Creature is not Creature cardOwner
            || !ColorCombatState.PlayerCreatures.Contains(cardOwner))
        {
            return Task.CompletedTask;
        }

        return ApplyColorToCard(card);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Enemy)
            return Task.CompletedTask;

        foreach (var player in ColorCombatState.Players)
            ClearColorMarks(player);

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.IsDupe || card.Owner.Creature == null || card.Owner.Creature == ColorOwner)
            return Task.CompletedTask;

        if (card.Affliction is not IColorMarkAffliction mark)
            return Task.CompletedTask;

        var data = GetInternalData<Data>();
        data.LastColors.Add(mark.Color);
        if (data.LastColors.Count > 2)
            data.LastColors.RemoveAt(0);

        if (data.LastColors.Count == 2)
        {
            bool firstMatches = data.LastColors[0] == data.RequiredColor;
            bool secondMatches = data.LastColors[1] == data.RequiredColor;
            if (firstMatches == secondMatches)
                return ResolveColor(choiceContext, data.LastColors[1], firstMatches);
        }

        return Task.CompletedTask;
    }

    protected abstract Task ResolveColor(
        PlayerChoiceContext choiceContext,
        ColorMarkType finalColor,
        bool matched);

    protected async Task AssignColorsToHand(PlayerChoiceContext choiceContext, Player player)
    {
        var data = GetInternalData<Data>();
        var hand = PileType.Hand.GetPile(player).Cards
            .Where(CanReceiveColor)
            .ToList();

        if (hand.Count == 0)
            return;

        var rng = ColorCombatState.Players.First().RunState.Rng.CombatCardSelection;
        var requiredCards = new HashSet<CardModel>();
        int requiredCardCount = Math.Min(2, hand.Count);
        while (requiredCards.Count < requiredCardCount)
            requiredCards.Add(hand[rng.NextInt(hand.Count)]);

        foreach (var card in hand)
            await ApplyColor(
                card,
                requiredCards.Contains(card) ? data.RequiredColor : GetRandomColor());
    }

    private async Task ApplyColorToCard(CardModel card)
    {
        if (AvailableColors.Count == 0)
            return;

        await ApplyColor(card, GetRandomColor());
    }

    private ColorMarkType GetRandomColor()
    {
        var rngPlayer = ColorCombatState.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("Color mechanic has no player RNG source.");
        return AvailableColors[
            rngPlayer.RunState.Rng.CombatCardSelection.NextInt(AvailableColors.Count)];
    }

    private static bool CanReceiveColor(CardModel card)
    {
        return card.Affliction is null
            && card.Type is not CardType.Status and not CardType.Curse
            && !card.Keywords.Contains(CardKeyword.Unplayable);
    }

    private static void ClearColorMarks(Player player)
    {
        foreach (var pileType in new[]
                 {
                     PileType.Draw,
                     PileType.Hand,
                     PileType.Discard,
                     PileType.Exhaust,
                     PileType.Play
                 })
        {
            foreach (var card in pileType.GetPile(player).Cards
                         .Where(card => card.Affliction is IColorMarkAffliction)
                         .ToList())
            {
                CardCmd.ClearAffliction(card);
            }
        }
    }

    private static async Task ApplyColor(CardModel card, ColorMarkType color)
    {
        var overlay = card.GetOrCreateCapability<ColorMarkOverlayCapability>();
        overlay.SetColor(color);

        var afflictionApplied = color switch
        {
            ColorMarkType.Red =>
                (await CardCmd.Afflict<RedColorMarkAffliction>(card, 1m)) is not null,
            ColorMarkType.Blue =>
                (await CardCmd.Afflict<BlueColorMarkAffliction>(card, 1m)) is not null,
            ColorMarkType.Yellow =>
                (await CardCmd.Afflict<YellowColorMarkAffliction>(card, 1m)) is not null,
            ColorMarkType.Purple =>
                (await CardCmd.Afflict<PurpleColorMarkAffliction>(card, 1m)) is not null,
            ColorMarkType.Green =>
                (await CardCmd.Afflict<GreenColorMarkAffliction>(card, 1m)) is not null,
            _ => false
        };

        if (!afflictionApplied)
        {
            card.RemoveCapability<ColorMarkOverlayCapability>();
        }
    }

    private async Task UpdateRequiredColorDisplay(
        PlayerChoiceContext choiceContext,
        ColorMarkType requiredColor)
    {
        var displayPower = ColorOwner.GetPower<RequiredColorPower>();
        if (displayPower is null)
        {
            displayPower = await PowerCmd.Apply<RequiredColorPower>(
                choiceContext,
                ColorOwner,
                (int)requiredColor,
                ColorOwner,
                null,
                true);
        }

        displayPower?.SetAmount((int)requiredColor, true);
    }

    protected async Task ApplyEffect(
        PlayerChoiceContext choiceContext,
        ColorMarkType color,
        bool matched,
        IReadOnlyList<Creature> players)
    {
        var enemies = ColorCombatState.GetCreaturesOnSide(CombatSide.Enemy);

        switch (color)
        {
            case ColorMarkType.Red:
                await CreatureCmd.Damage(
                    choiceContext,
                    matched ? enemies : players,
                    RedDamage,
                    ValueProp.Unpowered,
                    ColorOwner);
                break;
            case ColorMarkType.Blue:
                foreach (var player in matched ? players : new[] { ColorOwner })
                    await CreatureCmd.GainBlock(player, BlueBlock, ValueProp.Unpowered, null, false);
                break;
            case ColorMarkType.Yellow:
                await PowerCmd.Apply<StrengthPower>(
                    choiceContext,
                    matched ? players : new[] { ColorOwner },
                    YellowStrength,
                    ColorOwner,
                    null,
                    false);
                break;
            case ColorMarkType.Purple:
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    matched ? enemies : players,
                    PurpleVulnerable,
                    ColorOwner,
                    null,
                    false);
                break;
            case ColorMarkType.Green:
                await PowerCmd.Apply<WeakPower>(
                    choiceContext,
                    matched ? enemies : players,
                    GreenWeak,
                    ColorOwner,
                    null,
                    false);
                break;
        }
    }

    private sealed class Data
    {
        public int AssignedRound = -1;
        public ColorMarkType RequiredColor;
        public List<ColorMarkType> LastColors { get; } = new();
    }
}

public sealed class MaxiColorPower : ColorMechanicPower
{
    protected override IReadOnlyList<ColorMarkType> AvailableColors { get; } =
        new[] { ColorMarkType.Red, ColorMarkType.Blue };

    protected override Task ResolveColor(
        PlayerChoiceContext choiceContext,
        ColorMarkType finalColor,
        bool matched)
    {
        return ApplyEffect(choiceContext, finalColor, matched, ColorCombatState.PlayerCreatures);
    }
}

public sealed class BlushColorPower : ColorMechanicPower
{
    protected override IReadOnlyList<ColorMarkType> AvailableColors { get; } =
        new[]
        {
            ColorMarkType.Red,
            ColorMarkType.Blue,
            ColorMarkType.Yellow,
            ColorMarkType.Purple,
            ColorMarkType.Green
        };

    protected override int RedDamage => 7;

    protected override int BlueBlock => 6;

    protected override int YellowStrength => 2;

    protected override int PurpleVulnerable => 2;

    protected override int GreenWeak => 2;

    protected override Task ResolveColor(
        PlayerChoiceContext choiceContext,
        ColorMarkType finalColor,
        bool matched)
    {
        return ApplyEffect(choiceContext, finalColor, matched, ColorCombatState.PlayerCreatures);
    }
}

public sealed class RequiredColorPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override LocString Description
    {
        get
        {
            var description = base.Description;
            description.Add("RequiredColor", GetColorName(Amount));
            return description;
        }
    }

    private static string GetColorName(int amount)
    {
        return amount switch
        {
            (int)ColorMarkType.Red => "红",
            (int)ColorMarkType.Blue => "蓝",
            (int)ColorMarkType.Yellow => "黄",
            (int)ColorMarkType.Purple => "紫",
            (int)ColorMarkType.Green => "绿",
            _ => "未指定"
        };
    }
}
