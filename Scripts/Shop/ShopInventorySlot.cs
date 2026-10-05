using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public partial class ShopInventorySlot : Control
{
    private MerchantInventory _inventory = null!;
    private MerchantEntry _entry = null!;
    private Control _visual = null!;
    private Button _button = null!;
    private Label _price = null!;
    private NRelic? _relic;
    private bool _shopOnly;
    private bool _buying;
    private bool _usedAnimation;
    private Vector2 _restScale;
    private Tween? _hoverTween;

    public void Initialize(MerchantInventory inventory, MerchantEntry entry, bool shopOnly = false)
    {
        _inventory = inventory;
        _entry = entry;
        _shopOnly = shopOnly;
    }

    public override void _Ready()
    {
        _restScale = Scale;
        MouseFilter = MouseFilterEnum.Ignore;
        var removal = _entry is MerchantCardRemovalEntry;
        _visual = ResourceLoader.Load<PackedScene>(removal
            ? "res://mod/Game/scenes/merchant/merchant_card_removal.tscn"
            : "res://mod/Game/scenes/merchant/merchant_relic.tscn").Instantiate<Control>();
        _visual.Scale = Vector2.One;
        AddChild(_visual);
        DisableInput(_visual);
        _price = _visual.GetNode<Label>("Cost/CostLabel");
        var hitbox = _visual.GetNode<Control>("Hitbox");
        _button = new Button
        {
            Position = hitbox.Position, Size = hitbox.Size,
            Flat = true, MouseFilter = MouseFilterEnum.Stop,
        };
        CustomShopOverlay.RemoveButtonFrames(_button);
        AddChild(_button);
        _button.Pressed += () => TaskHelper.RunSafely(Buy());
        _button.MouseEntered += Hover;
        _button.MouseExited += Unhover;
        _entry.EntryUpdated += Refresh;
        _inventory.Player.GoldChanged += Refresh;
        VisibilityChanged += ClearHiddenHover;
        if (_entry is MerchantCardRemovalEntry removalEntry &&
            !Hook.ShouldAllowMerchantCardRemoval(_inventory.Player.RunState, _inventory.Player))
            removalEntry.SetUsed();
        Refresh();
    }

    private static void DisableInput(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.FocusMode = FocusModeEnum.None;
        }
        foreach (var child in node.GetChildren())
            DisableInput(child);
    }

    private void Refresh()
    {
        if (_entry is MerchantRelicEntry relicEntry)
        {
            Visible = relicEntry.IsStocked;
            if (_relic?.Model != relicEntry.Model)
            {
                _relic?.QueueFree();
                _relic = null;
                if (relicEntry.Model is { } model)
                {
                    _relic = NRelic.Create(model, NRelic.IconSize.Small);
                    if (_relic != null)
                    {
                        _visual.GetNode<Control>("RelicHolder").AddChild(_relic);
                        DisableInput(_relic);
                    }
                }
            }
        }
        else if (_entry is MerchantCardRemovalEntry { Used: true } && !_usedAnimation)
        {
            _usedAnimation = true;
            _visual.GetNode<AnimationPlayer>("Animation").Play("Used");
        }
        _visual.GetNode<Control>("Cost").Visible = _entry.IsStocked;
        _price.Text = _entry.Cost.ToString();
        _price.Modulate = _entry.EnoughGold ? Colors.White : Colors.Red;
        _button.Disabled = _buying || !_entry.IsStocked;
        if (!_entry.IsStocked)
            ResetHover();
        else if (!_buying && IsVisibleInTree() &&
            new Rect2(Vector2.Zero, _button.Size).HasPoint(_button.GetLocalMousePosition()))
            ShowTip();
    }

    private void ShowTip()
    {
        NHoverTipSet.Remove(this);
        NHoverTipSet? tips;
        if (_entry is MerchantRelicEntry { Model: { } model })
            tips = NHoverTipSet.CreateAndShow(this, model.HoverTips, HoverTipAlignment.None);
        else
        {
            var description = new LocString("merchant_room", "MERCHANT.cardRemovalService.description");
            description.Add("Amount", MerchantCardRemovalEntry.PriceIncrease);
            tips = NHoverTipSet.CreateAndShow(this, new HoverTip(
                new LocString("merchant_room", "MERCHANT.cardRemovalService.title"), description),
                HoverTipAlignment.None);
        }
        tips?.SetAlignment(_button, HoverTip.GetHoverTipAlignment(this, 0.5f));
    }

    private void Hover()
    {
        if (!_entry.IsStocked || _buying || !IsVisibleInTree())
            return;
        _hoverTween?.Kill();
        Scale = _restScale * (0.8f / 0.65f);
        ZIndex = 1;
        ShowTip();
    }

    private void Unhover()
    {
        NHoverTipSet.Remove(this);
        _hoverTween?.Kill();
        ZIndex = 0;
        if (!IsVisibleInTree())
        {
            ResetHover();
            return;
        }
        _hoverTween = CreateTween();
        _hoverTween.TweenProperty(this, "scale", _restScale, 0.5)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private void ClearHiddenHover()
    {
        if (!IsVisibleInTree())
            ResetHover();
    }

    private void ResetHover()
    {
        NHoverTipSet.Remove(this);
        _hoverTween?.Kill();
        _hoverTween = null;
        Scale = _restScale;
        ZIndex = 0;
    }

    private async Task Buy()
    {
        var shop = CustomShopOverlay.Instance;
        var player = _inventory.Player;
        if (_buying || !IsVisibleInTree() || !_entry.IsStocked ||
            player.Creature.IsDead || shop == null || shop.IsPurchasing)
            return;
        if (!_entry.EnoughGold)
        {
            shop.ShowPurchaseFailure(PurchaseStatus.FailureGold);
            return;
        }
        if (!shop.TryBeginPurchase())
            return;
        _buying = true;
        ResetHover();
        Refresh();
        try
        {
            if (_entry is MerchantCardRemovalEntry removal)
            {
                if (!Hook.ShouldAllowMerchantCardRemoval(player.RunState, player) ||
                    !player.Deck.Cards.Any(card => card.IsRemovable))
                    return;
                // Official selection handles cancel, payment, removal VFX, price progression and peers.
                if (await removal.OnTryPurchaseWrapper(_inventory, false, true))
                {
                    removal.SetUsed();
                    removal.OnMerchantInventoryUpdated();
                }
            }
            else if (_entry is MerchantRelicEntry { Model: { } model } relicEntry)
            {
                await PurchaseRelic(relicEntry, model);
            }
        }
        finally
        {
            _buying = false;
            shop.EndPurchase();
            if (IsInsideTree())
                Refresh();
        }
    }

    private async Task PurchaseRelic(MerchantRelicEntry entry, RelicModel model)
    {
        var player = _inventory.Player;
        // Capture price before obtaining Membership Card/Courier changes merchant discounts.
        var cost = entry.Cost;
        var position = _relic?.Icon.GlobalPosition ?? GlobalPosition;
        await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
        player.RunState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).BoughtRelics.Add(model.Id);
        await RelicCmd.Obtain(model, player, -1);
        RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(cost);
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(model);
        NRun.Instance?.GlobalUi.RelicInventory.AnimateRelic(model, position, null);

        if (Hook.ShouldRefillMerchantEntry(player.RunState, entry, player))
        {
            if (_shopOnly)
            {
                var blacklist = _inventory.RelicEntries.Select(e => e.Model?.CanonicalInstance)
                    .OfType<RelicModel>().ToHashSet();
                AccessTools.Method(typeof(MerchantRelicEntry), "FillSlot")
                    .Invoke(entry, [RelicRarity.Shop, blacklist]);
            }
            else
                AccessTools.Method(typeof(MerchantRelicEntry), "RestockAfterPurchase")
                    .Invoke(entry, [_inventory]);
        }
        else
            AccessTools.Method(typeof(MerchantRelicEntry), "ClearAfterPurchase").Invoke(entry, null);
        await Hook.AfterItemPurchased(player.RunState, player, entry, cost);
        entry.InvokePurchaseCompleted(entry);
        if (player.RunState.CurrentRoom is MerchantRoom room)
        {
            foreach (var gem in ShopGemSync.GetEntries(room, player))
                gem.OnMerchantInventoryUpdated();
        }
    }

    public override void _ExitTree()
    {
        ResetHover();
        _entry.EntryUpdated -= Refresh;
        _inventory.Player.GoldChanged -= Refresh;
    }
}
