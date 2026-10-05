using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public partial class CustomShopOverlay : Control, IScreenContext
{
    public static CustomShopOverlay? Instance { get; private set; }
    public Control? DefaultFocusedControl { get; private set; }
    private static readonly StringName ShopViewportPath = "ShopViewport";
    private MerchantRoom? _room;
    private NMapScreen? _map;
    [Export] public Rect2 MerchantHeadRegion { get; set; } = new(-110, -365, 270, 157.5f);
    [Export] public Vector2 MerchantDialoguePosition { get; set; } = new(320, 640);
    private Control _merchantHeadHitbox = null!;
    private MegaSprite _merchantSprite = null!;
    private bool _isStroking;
    private NMerchantDialogue _merchantDialogue = null!;
    public bool IsPurchasing { get; private set; }

    public bool TryBeginPurchase()
    {
        if (IsPurchasing)
            return false;
        IsPurchasing = true;
        _map?.SetTravelEnabled(false);
        return true;
    }

    public void EndPurchase()
    {
        IsPurchasing = false;
        _map?.SetTravelEnabled(true);
    }

    public void Initialize(MerchantRoom room) => _room = room;

    public override void _Ready()
    {
        Instance = this;
        MouseFilter = MouseFilterEnum.Ignore;
        HideOfficialPlaceholders();
        PopulateCards();
        PopulateGems();
        PopulateRelicsAndRemoval();
        CreateOwnProceedButton();
        _map = NMapScreen.Instance;
        if (_map != null)
        {
            _map.SetTravelEnabled(true);
            _map.Connect(NMapScreen.SignalName.Opened, Callable.From(OnMapOpened));
            _map.Connect(NMapScreen.SignalName.Closed, Callable.From(OnMapClosed));
            Visible = !_map.IsOpen;
        }
        Resized += FitScene;
        FitScene();
        var merchant = GetNode<Node2D>("ShopViewport/MerchantVisual");
        _merchantSprite = new MegaSprite(merchant);
        _merchantSprite.GetAnimationState().SetAnimation("idle_loop", true);
        _merchantHeadHitbox = new Control
        {
            Name = "HeadStrokeHitbox",
            Position = MerchantHeadRegion.Position,
            Size = MerchantHeadRegion.Size,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        merchant.AddChild(_merchantHeadHitbox);
        CreateMerchantDialogue();
        VisibilityChanged += ResetMerchantStroke;
    }

    private void CreateMerchantDialogue()
    {
        _merchantDialogue = ResourceLoader.Load<PackedScene>(
            "res://scenes/merchant/merchant_rug_dialogue.tscn").Instantiate<NMerchantDialogue>();
        GetNode<Control>("ShopViewport").AddChild(_merchantDialogue);
        _merchantDialogue.ZIndex = 5;
        var text = _merchantDialogue.GetNode<RichTextLabel>("%Text");
        text.AddThemeFontOverride("normal_font", text.GetThemeFont("bold_font"));
        DisableInput(_merchantDialogue);
        _merchantDialogue.Initialize(MerchantDialogueSet.CreateFromLocStrings(new[]
        {
            new LocString("merchant_room", "RNS_MIO.purchaseFailureGold.0"),
            new LocString("merchant_room", "RNS_MIO.purchaseFailureForbidden.0"),
        }));
        _merchantDialogue.Hide();
    }

    public void ShowPurchaseFailure(PurchaseStatus status)
    {
        if (!IsVisibleInTree())
            return;
        _merchantDialogue.Show();
        _merchantDialogue.ShowForPurchaseAttempt(status);
        // The official dialogue randomizes X over the rug; anchor it to Mio instead.
        _merchantDialogue.Position = MerchantDialoguePosition;
    }

    public override void _Process(double delta)
    {
        if (!CanStrokeMerchant() || !IsMouseOverMerchantHead())
        {
            ResetMerchantStroke();
            return;
        }

        if (_isStroking)
            return;
        _isStroking = true;
        _merchantSprite.GetAnimationState().SetAnimation("middle_sigh", true);
    }

    private bool CanStrokeMerchant() =>
        IsVisibleInTree() && !IsPurchasing && GetWindow().HasFocus() && !(_map?.IsOpen ?? false);

    private bool IsMouseOverMerchantHead() =>
        new Rect2(Vector2.Zero, _merchantHeadHitbox.Size)
            .HasPoint(_merchantHeadHitbox.GetLocalMousePosition());

    private void ResetMerchantStroke()
    {
        if (!_isStroking)
            return;
        _isStroking = false;
        _merchantSprite.GetAnimationState().SetAnimation("idle_loop", true);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_map))
        {
            _map!.Disconnect(NMapScreen.SignalName.Opened, Callable.From(OnMapOpened));
            _map.Disconnect(NMapScreen.SignalName.Closed, Callable.From(OnMapClosed));
        }
        if (Instance == this)
            Instance = null;
    }

    private void OnMapOpened()
    {
        _merchantDialogue.Hide();
        Hide();
    }
    private void OnMapClosed() => Show();

    private void HideOfficialPlaceholders()
    {
        var paths = new[]
        {
            "ShopViewport/SlotsContainer/MerchantCardRemoval",
            "ShopViewport/SlotsContainer/MerchantPotion",
            "ShopViewport/SlotsContainer/MerchantPotion2",
            "ShopViewport/SlotsContainer/MerchantRelic",
            "ShopViewport/SlotsContainer/MerchantRelic2",
            "ShopViewport/SlotsContainer/MerchantRelic3",
            "ShopViewport/SlotsContainer/MerchantGem",
            "ShopViewport/SlotsContainer/MerchantGem2",
            "ShopViewport/SlotsContainer/MerchantGem3",
            "ShopViewport/SlotsContainer/MerchantGem4",
        };

        foreach (var path in paths)
        {
            if (GetNodeOrNull<CanvasItem>(path) is not { } node)
                continue;

            node.Visible = false;
            node.ProcessMode = ProcessModeEnum.Disabled;
            if (node is Control control)
                control.MouseFilter = MouseFilterEnum.Ignore;
        }
    }

    private void FitScene()
    {
        var viewport = GetNode<Control>("ShopViewport");
        viewport.PivotOffset = new Vector2(960, 540);
        viewport.Scale = Vector2.One * Mathf.Min(Size.X / 1920f, Size.Y / 1080f);
    }

    private void PopulateCards()
    {
        var inventory = _room?.GetLocalInventory();
        if (inventory == null)
        {
            Visible = false;
            return;
        }

        var entries = inventory.CardEntries
            .Where(entry => entry.IsStocked && entry.CreationResult?.Card != null)
            .ToArray();

        var productScene = ResourceLoader.Load<PackedScene>("res://mod/Sence/Shop/merchant_gem.tscn");
        for (var i = 0; i < 6; i++)
        {
            var slotName = i == 0 ? "MerchantCard" : $"MerchantCard{i + 1}";
            var slot = GetNode<Control>($"{ShopViewportPath}/SlotsContainer/{slotName}");
            // Preserve authored card centers and the slot's single 0.65 scale.
            DisableInput(slot);
            foreach (var child in slot.GetChildren().OfType<CanvasItem>())
                child.Hide();
            var gem = productScene.Instantiate<CustomMerchantGem>();
            gem.Name = "PurchasableCard";
            gem.Scale = Vector2.One;
            slot.AddChild(gem);
            gem.AlignToCardSlot();
            gem.BindSaleVisual(slot.GetNode<CanvasItem>("SaleVisual"));
            if (i < entries.Length)
                gem.Fill(entries[i]);
            else
                gem.Visible = false;
        }
    }

    private void CreateOwnProceedButton()
    {
        var left = GetNode<Control>("ShopViewport/ProceedButtonLeft");
        left.Hide();
        left.ProcessMode = ProcessModeEnum.Disabled;
        DisableInput(left);

        var holder = GetNode<Control>("ShopViewport/ProceedButton");
        DisableInput(holder);
        holder.GetNodeOrNull<CanvasItem>("HotkeyIcon")?.Hide();
        holder.GetNodeOrNull<Node>("HotkeyIcon")?.SetProcess(false);
        foreach (var label in holder.FindChildren("*", "Label", true, false).OfType<Label>())
            label.Text = "前进";
        var button = new Button
        {
            Name = "CustomProceedHitbox",
            Flat = true,
            MouseFilter = MouseFilterEnum.Stop,
        };
        holder.AddChild(button);
        RemoveButtonFrames(button);
        button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var restScale = holder.Scale;
        var restModulate = holder.Modulate;
        button.MouseEntered += () =>
        {
            holder.Scale = restScale * 1.08f;
            holder.Modulate = restModulate * new Color(1.15f, 1.15f, 1.15f, 1f);
        };
        button.MouseExited += () =>
        {
            holder.Scale = restScale;
            holder.Modulate = restModulate;
        };
        holder.VisibilityChanged += () =>
        {
            if (!holder.IsVisibleInTree())
            {
                holder.Scale = restScale;
                holder.Modulate = restModulate;
            }
        };
        button.Pressed += LeaveShop;
        DefaultFocusedControl = button;
    }

    private void PopulateGems()
    {
        if (_room?.GetLocalInventory() is not { } inventory)
            return;
        var entries = ShopGemSync.GetEntries(_room, inventory.Player);
        var container = GetNode<Control>("ShopViewport/SlotsContainer");
        for (var i = 0; i < entries.Length; i++)
        {
            var name = i == 0 ? "MerchantGem" : $"MerchantGem{i + 1}";
            var placeholder = container.GetNode<Control>(name);
            var slot = new ShopGemSlot
            {
                Name = $"{name}Product",
                Position = placeholder.Position,
                Size = placeholder.Size,
                Scale = placeholder.Scale,
            };
            slot.Initialize(entries[i], _room);
            container.AddChild(slot);
        }
    }

    private void PopulateRelicsAndRemoval()
    {
        if (_room?.GetLocalInventory() is not { } inventory)
            return;
        var container = GetNode<Control>("ShopViewport/SlotsContainer");
        var names = new[] { "MerchantRelic", "MerchantRelic2", "MerchantRelic3",
            "MerchantPotion", "MerchantPotion2" };
        var indices = new[] { 0, 1, 3, 4, 2 };
        for (var i = 0; i < names.Length; i++)
        {
            var placeholder = container.GetNode<Control>(names[i]);
            var slot = new ShopInventorySlot
            {
                Name = $"RelicProduct{i + 1}", Position = placeholder.Position,
                Size = placeholder.Size, Scale = placeholder.Scale,
            };
            slot.Initialize(inventory, inventory.RelicEntries[indices[i]], i == 4);
            container.AddChild(slot);
        }
        if (inventory.CardRemovalEntry is { } removal)
        {
            var placeholder = container.GetNode<Control>("MerchantCardRemoval");
            var slot = new ShopInventorySlot
            {
                Name = "RemovalService", Position = placeholder.Position,
                Size = placeholder.Size, Scale = placeholder.Scale,
            };
            slot.Initialize(inventory, removal);
            container.AddChild(slot);
        }
    }

    private static void DisableInput(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.FocusMode = FocusModeEnum.None;
            control.TooltipText = "";
        }
        foreach (var child in node.GetChildren())
            DisableInput(child);
    }

    public static void RemoveButtonFrames(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        button.TooltipText = "";
    }
    private void LeaveShop()
    {
        if (!IsPurchasing)
        {
            if (_map == null)
                return;
            _merchantDialogue.Hide();
            Hide();
            _map.Open(false);
        }
    }
}
