using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace RabbitAndSteelNewMap.Scripts.Enchantment;

public abstract partial class ShopGemEnchantment
{
    public int BattleLimit => Kind == GemKind.Basic ? Color switch
    {
        GemColor.Blue => 3,
        GemColor.Red => 2,
        _ => 0,
    } : 0;

    private int _battlesCompleted;
    [SavedProperty]
    public int BattlesCompleted
    {
        get => _battlesCompleted;
        set
        {
            AssertMutable();
            _battlesCompleted = value;
            RefreshDescriptionValues();
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Combats", BattleLimit),
        new IntVar("SameName", 1),
        new BlockVar(2, ValueProp.Move),
    ];

    public void RefreshDescriptionValues()
    {
        DynamicVars["Combats"].BaseValue = Math.Max(0, BattleLimit - BattlesCompleted);
        DynamicVars["SameName"].BaseValue = HasCard ? CountSameName(Card) : 1;
    }

    public override decimal EnchantDamageAdditive(decimal originalDamage, ValueProp props) =>
        Kind == GemKind.Attack && Color == GemColor.Purple && props.IsPoweredAttack() ? 4 : 0;

    public override decimal EnchantDamageMultiplicative(decimal originalDamage, ValueProp props) =>
        Kind == GemKind.Attack && props.IsPoweredAttack() ? Color switch
        {
            GemColor.Blue => 0.7m,
            GemColor.Green => 1.5m,
            _ => 1m,
        } : 1m;

    public override int EnchantPlayCount(int originalPlayCount) => originalPlayCount + ((Kind, Color) switch
    {
        (GemKind.Attack, GemColor.Blue) => 1,
        (GemKind.Power, GemColor.Red) => 1,
        (GemKind.Basic, GemColor.Green) => 2,
        _ => 0,
    });

    // Derived from the current enchantment, not saved into the card's original keywords.
    public CardKeyword? GrantedKeyword => (Kind, Color) switch
    {
        (GemKind.Power, GemColor.Purple) => CardKeyword.Retain,
        (GemKind.Basic, GemColor.Red) => CardKeyword.Unplayable,
        _ => null,
    };

    public int CostAdjustment => (Kind, Color) switch
    {
        (GemKind.Attack, GemColor.Yellow) or (GemKind.Power, GemColor.Blue) => -1,
        (GemKind.Attack, GemColor.Green) or (GemKind.Power, GemColor.Red) => 1,
        (GemKind.Basic, GemColor.Purple) => HasCard ? -CountSameName(Card) : -1,
        _ => 0,
    };

    public static bool SameName(CardModel a, CardModel b) =>
        a.Id == b.Id ||
        (a.Tags.Contains(CardTag.Strike) && b.Tags.Contains(CardTag.Strike)) ||
        (a.Tags.Contains(CardTag.Defend) && b.Tags.Contains(CardTag.Defend));

    public static int CountSameName(CardModel card)
    {
        var state = card.Owner?.PlayerCombatState;
        if (state == null)
            return 1;
        return state.AllPiles
            .Where(p => p.Type is PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust)
            .SelectMany(p => p.Cards)
            .Append(card)
            .Distinct()
            .Count(c => SameName(card, c));
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (!HasCard || card.Owner != Card.Owner)
            return true;
        if (Kind == GemKind.Basic && Color == GemColor.Red && card == Card)
            return false;
        if (Kind != GemKind.Basic || Color != GemColor.Green ||
            Card.Pile?.Type != PileType.Hand || card == Card)
            return true;
        // A green basic gem makes every other hand card wait until a green-gem card
        // is played. Multiple green-gem cards remain mutually playable.
        return card.Enchantment is ShopGemEnchantment { Kind: GemKind.Basic, Color: GemColor.Green };
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        var owner = Card.Owner;
        if (Kind == GemKind.Attack && Color == GemColor.Red)
            await PowerCmd.Apply<StrengthPower>(choiceContext, owner.Creature, 2, owner.Creature, Card);
        if (Kind == GemKind.Skill)
        {
            switch (Color)
            {
                case GemColor.Blue:
                    await CreatureCmd.GainBlock(owner.Creature, DynamicVars.Block, cardPlay);
                    break;
                case GemColor.Yellow:
                    await PowerCmd.Apply<DexterityPower>(choiceContext, owner.Creature, 1, owner.Creature, Card);
                    break;
                case GemColor.Green:
                case GemColor.Red:
                    var enemies = Card.CombatState!.GetOpponentsOf(owner.Creature).Where(c => c.IsAlive).ToArray();
                    if (Color == GemColor.Green)
                        await PowerCmd.Apply<WeakPower>(choiceContext, enemies, 3, owner.Creature, Card);
                    else
                        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemies, 3, owner.Creature, Card);
                    break;
            }
        }
        if (Color != GemColor.Yellow || owner.PlayerCombatState == null)
            return;
        var hand = PileType.Hand.GetPile(owner).Cards.Where(c => c != Card).ToArray();
        if (Kind == GemKind.Power && hand.Length > 0)
            MakeFreeThisTurn(owner.RunState.Rng.CombatCardSelection.NextItem(hand)!);
        else if (Kind == GemKind.Basic)
            foreach (var other in hand.Where(c => SameName(Card, c)))
                MakeFreeThisTurn(other);
    }

    private static void MakeFreeThisTurn(CardModel card)
    {
        card.EnergyCost.SetThisTurn(0);
        card.SetStarCostThisTurn(0);
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == Card && Kind == GemKind.Skill && Color == GemColor.Purple)
            await CardPileCmd.Draw(choiceContext, 1, Card.Owner);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        // Like vanilla Guilty: tick only the permanent deck instance, never its combat copy.
        if (!HasCard || BattleLimit == 0 || Card.Pile?.Type != PileType.Deck)
            return;
        BattlesCompleted++;
        if (BattlesCompleted >= BattleLimit)
            await CardPileCmd.RemoveFromDeck(Card);
    }

    public async Task ApplyPurchaseCopies()
    {
        var copies = (Kind, Color) switch
        {
            (GemKind.Power, GemColor.Green) => 1,
            (GemKind.Basic, GemColor.Purple) => 4,
            _ => 0,
        };
        for (var i = 0; i < copies; i++)
        {
            var copy = Card.Owner.RunState.CloneCard(Card);
            CardCmd.PreviewCardPileAdd(
                await CardPileCmd.Add(copy, PileType.Deck, CardPilePosition.Bottom),
                1.2f, CardPreviewStyle.HorizontalLayout);
        }
    }
}
