using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Rooms;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public partial class ShopGemSlot : Control
{
    private ShopGemEntry _entry = null!;
    private MerchantRoom _room = null!;
    private TextureRect _icon = null!;
    private Button _button = null!;
    private Label _price = null!;
    private bool _buying;
    private Vector2 _restScale;
    private int _restZIndex;
    private Tween? _hoverTween;

    public void Initialize(ShopGemEntry entry, MerchantRoom room)
    {
        _entry = entry;
        _room = room;
    }

    public override void _Ready()
    {
        _restScale = Scale;
        _restZIndex = ZIndex;
        MouseFilter = MouseFilterEnum.Ignore;
        _icon = new TextureRect
        {
            Position = new Vector2(-20, -100), Size = new Vector2(162, 210),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);
        _button = new Button
        {
            Position = _icon.Position, Size = _icon.Size, Flat = true,
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(_button);
        CustomShopOverlay.RemoveButtonFrames(_button);
        var cost = new HBoxContainer
        {
            Position = new Vector2(-42, 128), Size = new Vector2(206, 54),
            Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(cost);
        cost.AddChild(new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://mod/Game/images/atlases/ui_atlas.sprites/top_bar/top_bar_gold.tres"),
            CustomMinimumSize = new Vector2(54, 54),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        _price = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _price.AddThemeFontSizeOverride("font_size", 39);
        cost.AddChild(_price);
        _button.Pressed += () => TaskHelper.RunSafely(Buy());
        _button.MouseEntered += OnMouseEntered;
        _button.MouseExited += OnMouseExited;
        VisibilityChanged += ClearHiddenHover;
        _entry.EntryUpdated += Refresh;
        _entry.Player.GoldChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        ResetHover();
        _entry.EntryUpdated -= Refresh;
        _entry.Player.GoldChanged -= Refresh;
    }

    private void OnMouseEntered()
    {
        if (!IsVisibleInTree() || !_entry.IsStocked)
            return;

        _hoverTween?.Kill();
        _hoverTween = null;
        Scale = _restScale * (0.8f / 0.65f);
        ZIndex = _restZIndex + 1;
        ShowHoverTip();
    }

    private void ShowHoverTip()
    {
        NHoverTipSet.Remove(this);
        var enchantment = _entry.Enchantment;
        var tips = NHoverTipSet.CreateAndShow(this,
            new HoverTip(enchantment.Title, enchantment.DynamicDescription), HoverTipAlignment.None);
        tips?.SetAlignment(_button, HoverTip.GetHoverTipAlignment(this, 0.5f));
    }

    private void OnMouseExited()
    {
        NHoverTipSet.Remove(this);
        _hoverTween?.Kill();
        _hoverTween = null;
        ZIndex = _restZIndex;
        if (!IsVisibleInTree())
        {
            Scale = _restScale;
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
        ZIndex = _restZIndex;
    }

    private void Refresh()
    {
        Visible = _entry.IsStocked;
        var enchantment = _entry.Enchantment;
        _icon.Texture = ResourceLoader.Load<Texture2D>(enchantment.TexturePath);
        _price.Text = _entry.Cost.ToString();
        _price.Modulate = _entry.Player.Gold < _entry.Cost ? Colors.Red : Colors.White;
        // Keep the hitbox active so unaffordable or currently ineligible stock
        // can still be inspected with the standard hover enlargement.
        _button.Disabled = _buying || !_entry.IsStocked;
        if (!_buying && IsVisibleInTree() &&
            new Rect2(Vector2.Zero, _button.Size).HasPoint(_button.GetLocalMousePosition()))
            ShowHoverTip();
    }

    private async Task Buy()
    {
        var shop = CustomShopOverlay.Instance;
        if (_buying || !IsVisibleInTree() || !_entry.IsStocked ||
            shop == null || shop.IsPurchasing || _entry.Player.Creature.IsDead)
            return;
        if (_entry.Player.Gold < _entry.Cost)
        {
            shop.ShowPurchaseFailure(PurchaseStatus.FailureGold);
            return;
        }
        if (!_entry.HasEligibleCards)
        {
            shop.ShowPurchaseFailure(PurchaseStatus.FailureForbidden);
            return;
        }
        if (!shop.TryBeginPurchase())
            return;
        ResetHover();
        _buying = true;
        Refresh();
        try
        {
            await ShopGemSync.PurchaseLocal(_room, _entry);
        }
        finally
        {
            _buying = false;
            shop.EndPurchase();
            Refresh();
        }
    }
}
