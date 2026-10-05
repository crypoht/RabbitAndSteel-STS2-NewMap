using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Random;
using RabbitAndSteelNewMap.Scripts.Enchantment;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public sealed class ShopGemEntry : MerchantEntry
{
    public GemKind Kind { get; }
    public GemColor Color { get; private set; }
    public int Revision { get; private set; }
    public override bool IsStocked => _stocked;
    public ShopGemEnchantment Enchantment => ShopGemEnchantment.Get(Kind, Color);
    public Player Player => _player;
    public bool HasEligibleCards => _player.Deck.Cards.Any(Enchantment.CanEnchantInShop);
    private bool _stocked;
    private bool _buying;
    private readonly Rng _rng;

    public ShopGemEntry(Player player, GemKind kind) : base(player)
    {
        Kind = kind;
        _rng = new Rng(player.RunState.Rng.Seed,
            $"rns_shop_gem:{player.NetId}:{player.RunState.CurrentActIndex}:{player.RunState.CurrentMapCoord}:{kind}");
        Populate();
    }

    private void Populate()
    {
        Color = (GemColor)_rng.NextInt(5);
        CalcCost();
        _stocked = true;
    }

    public override void CalcCost() => _cost = _rng.NextInt(75, 101);
    protected override void ClearAfterPurchase() => _stocked = false;
    protected override void RestockAfterPurchase(MerchantInventory? inventory) => Populate();
    protected override Task<(bool, int)> OnTryPurchase(MerchantInventory? inventory, bool ignoreCost) =>
        throw new NotSupportedException("Gem purchases must use the synchronized selection flow.");

    public async Task<bool> Purchase(int expectedRevision, GemColor expectedColor, int finalCost,
        MerchantInventory inventory)
    {
        if (_buying || !_stocked || expectedRevision != Revision || expectedColor != Color ||
            finalCost < 0 || _player.Gold < finalCost ||
            _player.Creature.IsDead || !HasEligibleCards)
            return false;

        _buying = true;
        try
        {
            var enchantment = Enchantment;
            // Generic selection honors manual confirmation even when only one card is eligible.
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };
            var selected = (await CardSelectCmd.FromDeckGeneric(
                _player, prefs, enchantment.CanEnchantInShop)).FirstOrDefault();
            if (selected == null || !enchantment.CanEnchantInShop(selected) ||
                !_player.Deck.Cards.Contains(selected) || _player.Gold < finalCost ||
                _player.Creature.IsDead)
                return false;

            if (selected.Enchantment is ShopGemEnchantment)
                CardCmd.ClearEnchantment(selected);
            var applied = (ShopGemEnchantment)enchantment.ToMutable();
            CardCmd.Enchant(applied, selected, 1);
            await PlayerCmd.LoseGold(finalCost, _player, GoldLossType.Spent);
            await applied.ApplyPurchaseCopies();
            if (Hook.ShouldRefillMerchantEntry(_player.RunState, this, _player))
                RestockAfterPurchase(inventory);
            else
                ClearAfterPurchase();
            Revision++;
            await Hook.AfterItemPurchased(_player.RunState, _player, this, finalCost);
            InvokePurchaseCompleted(this);
            return true;
        }
        finally
        {
            _buying = false;
            OnMerchantInventoryUpdated();
        }
    }
}
