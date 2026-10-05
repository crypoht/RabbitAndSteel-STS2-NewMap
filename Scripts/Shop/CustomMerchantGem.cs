using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public partial class CustomMerchantGem : Control
{
    private const int HealAmount = 5;
    // Reuse the official stock transitions without invoking vanilla shop UI/purchase handlers.
    private static readonly System.Reflection.MethodInfo ClearStock =
        AccessTools.Method(typeof(MerchantCardEntry), "ClearAfterPurchase");
    private static readonly System.Reflection.MethodInfo Restock =
        AccessTools.Method(typeof(MerchantCardEntry), "RestockAfterPurchase");

    private MerchantCardEntry? _entry;
    private Player? _player;
    private CanvasItem? _saleVisual;
    private CardModel? _card;
    private NCard? _cardNode;
    private Control _cardHolder = null!;
    private Control _hitbox = null!;
    private Label _costLabel = null!;
    private bool _purchased;
    private bool _purchasing;
    private bool _cardSlotLayout;
    private Tween? _hoverTween;
    private Vector2 _restScale;

    public override void _Ready()
    {
        _restScale = Scale;
        _cardHolder = GetNode<Control>("%GemHolder");
        _hitbox = GetNode<Control>("%Hitbox");
        _costLabel = GetNode<Label>("%CostLabel");
        _hitbox.GuiInput += OnGuiInput;
        _hitbox.MouseEntered += OnMouseEntered;
        _hitbox.MouseExited += OnMouseExited;
        VisibilityChanged += ClearHiddenHover;
        Visible = false;
    }

    public void AlignToCardSlot()
    {
        _cardSlotLayout = true;
        _restScale = Scale;
        MouseFilter = MouseFilterEnum.Ignore;
        _cardHolder.Position = Vector2.Zero;
        _cardHolder.Scale = Vector2.One;
        _hitbox.Position = new Vector2(-150, -211);
        _hitbox.Size = new Vector2(300, 422);
        var cost = GetNode<Control>("Cost");
        cost.Position = new Vector2(-150, 220);
        cost.Size = new Vector2(300, 58);
    }

    public void BindSaleVisual(CanvasItem saleVisual)
    {
        // Keep the scene-authored local transform while following product hover scaling.
        saleVisual.Reparent(this, false);
        _saleVisual = saleVisual;
        _saleVisual.Visible = false;
    }

    private void ClearHiddenHover()
    {
        if (!IsVisibleInTree())
        {
            NHoverTipSet.Remove(this);
            _hoverTween?.Kill();
            _hoverTween = null;
            Scale = _restScale;
            ZIndex = 0;
        }
    }

    public override void _ExitTree()
    {
        DisconnectEntry();
        _hoverTween?.Kill();
        _hoverTween = null;
        NHoverTipSet.Remove(this);
    }

    private void DisconnectEntry()
    {
        if (_entry != null)
            _entry.EntryUpdated -= UpdateVisual;
        if (_player != null)
            _player.GoldChanged -= UpdateVisual;
    }

    public void Fill(MerchantCardEntry entry)
    {
        DisconnectEntry();
        _entry = entry;
        _card = entry.CreationResult?.Card;
        _player = _card?.Owner;
        _entry.EntryUpdated += UpdateVisual;
        if (_player != null)
            _player.GoldChanged += UpdateVisual;
        _purchased = false;
        _purchasing = false;
        BuildCardVisual();
        UpdateVisual();
    }

    private void BuildCardVisual()
    {
        _cardNode?.QueueFree();
        _cardNode = null;

        if (_card == null)
            return;

        var cardNode = NCard.Create(_card, ModelVisibility.Visible);
        if (cardNode == null)
            return;

        _cardNode = cardNode;
        cardNode.Scale = Vector2.One * (_cardSlotLayout ? 1f : 0.7f);
        _cardHolder.AddChild(cardNode);
        cardNode.Position = _cardSlotLayout ? Vector2.Zero : new Vector2(176f, 220f);
        cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
    }

    private void OnGuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed)
            return;

        TaskHelper.RunSafely(TryPurchase());
        AcceptEvent();
    }

    private async Task TryPurchase()
    {
        if (!IsVisibleInTree() || _purchasing || _purchased || _entry == null ||
            _card?.Owner is not { } player)
            return;

        var shop = CustomShopOverlay.Instance;
        if (shop == null || !shop.TryBeginPurchase())
            return;
        _purchasing = true;
        try
        {
            // Snapshot the official final price once for validation, payment and sync.
            var cost = _entry.Cost;
            if (player.Creature.IsDead)
                return;
            if (player.Gold < cost)
            {
                shop.ShowPurchaseFailure(PurchaseStatus.FailureGold);
                return;
            }

            var result = await CardPileCmd.Add(
                _card,
                PileType.Deck,
                CardPilePosition.Bottom,
                null,
                false);

            if (!result.success)
                return;

            PlayPurchaseAnimation();
            await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
            RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(cost);
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(_card);
            if (_card.Pool is ColorlessCardPool)
                player.RunState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId)
                    .BoughtColorless.Add(_card.Id);

            NHoverTipSet.Remove(this);
            if (player.RunState.CurrentRoom is MerchantRoom room &&
                Hook.ShouldRefillMerchantEntry(player.RunState, _entry, player))
                Restock.Invoke(_entry, [room.GetLocalInventory()]);
            else
                ClearStock.Invoke(_entry, null);

            await Hook.AfterItemPurchased(player.RunState, player, _entry, cost);
            _entry.InvokePurchaseCompleted(_entry);
            UpdateVisual();
            // The vanilla shop heal animation requires an NMerchantRoom, which we do not create.
            await CreatureCmd.Heal(player.Creature, HealAmount, false);
        }
        finally
        {
            _purchasing = false;
            shop.EndPurchase();
        }
    }

    private void PlayPurchaseAnimation()
    {
        if (_cardNode is not { } cardNode || NRun.Instance is not { } run ||
            cardNode.Model?.Owner is not { } owner)
            return;

        NHoverTipSet.Remove(this);
        var scale = cardNode.GetGlobalTransform().Scale;
        run.GlobalUi.ReparentCard(cardNode);
        cardNode.Scale = Vector2.One;
        cardNode.Scale = scale / cardNode.GetGlobalTransform().Scale;
        _cardNode = null;
        var vfx = NCardFlyVfx.Create(cardNode, PileType.Deck, true, owner.Character.TrailPath);
        if (vfx != null)
            run.GlobalUi.TopBar.TrailContainer.AddChild(vfx);
        else
            cardNode.QueueFree();
    }

    private void UpdateVisual()
    {
        var currentCard = _entry?.CreationResult?.Card;
        if (!ReferenceEquals(_card, currentCard))
        {
            NHoverTipSet.Remove(this);
            _card = currentCard;
            BuildCardVisual();
        }
        _purchased = _entry?.IsStocked != true;
        Visible = _card != null && !_purchased;
        var cost = _entry?.Cost ?? 0;
        _costLabel.Text = cost.ToString();
        if (_saleVisual != null)
            _saleVisual.Visible = _entry?.IsOnSale == true && !_purchased;

        if (_card?.Owner is not { } player)
            return;

        _costLabel.Modulate = player.Gold < cost ? Colors.Red :
            _entry?.IsOnSale == true ? Colors.LightGreen : Colors.White;
    }

    private void OnMouseEntered()
    {
        UpdateVisual();
        if (_card == null || _purchased || !IsVisibleInTree())
            return;

        _hoverTween?.Kill();
        _hoverTween = null;
        Scale = _restScale * (0.8f / 0.65f);
        ZIndex = 1;
        var tips = NHoverTipSet.CreateAndShow(this, _card.HoverTips, HoverTipAlignment.None);
        tips?.SetAlignment(_hitbox, HoverTip.GetHoverTipAlignment(this, 0.75f));
    }

    private void OnMouseExited()
    {
        NHoverTipSet.Remove(this);
        _hoverTween?.Kill();
        ZIndex = 0;
        if (!IsVisibleInTree())
        {
            Scale = _restScale;
            _hoverTween = null;
            return;
        }
        _hoverTween = CreateTween();
        _hoverTween.TweenProperty(this, "scale", _restScale, 0.5)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }
}
