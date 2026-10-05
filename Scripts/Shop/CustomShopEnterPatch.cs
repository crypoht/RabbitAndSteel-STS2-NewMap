using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using STS2RitsuLib.Patching.Models;
using RabbitAndSteelNewMap.Scripts.Map;
using MegaCrit.Sts2.Core.Factories;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public sealed class CustomShopEnterPatch : IPatchMethod
{
    public static string PatchId => "rabbit_custom_shop_enter";
    public static bool IsCritical => true;
    public static string Description => "Enter Mio's shop without instantiating the vanilla merchant scene";
    public static ModPatchTarget[] GetTargets() => [new(typeof(MerchantRoom), "EnterInternal")];

    public static bool Prefix(MerchantRoom __instance, IRunState? runState,
        bool isRestoringRoomStackBase, ref Task __result)
    {
        if (runState?.CurrentMapCoord is not { } coord ||
            !CustomMapNodeRegistry.TryGetKind(runState, runState.CurrentActIndex, coord, out var kind) ||
            kind != CustomMapNodeKind.ShopLike)
            return true;

        __result = Enter(__instance, runState, isRestoringRoomStackBase);
        return false;
    }

    private static async Task Enter(MerchantRoom room, IRunState? state, bool restoring)
    {
        if (restoring)
            throw new InvalidOperationException("Merchant room stack reconstruction is not supported.");
        if (state == null)
            throw new InvalidOperationException("Mio shop requires a run state.");

        // Preserve the data required by MerchantRoom.Exit, without creating NMerchantRoom.
        AccessTools.Field(typeof(MerchantRoom), "_runState").SetValue(room, state);
        room.Inventories.Clear();
        foreach (var player in state.Players)
        {
            var inventory = MerchantInventory.CreateForNormalMerchant(player);
            // Keep the existing shop relic at index 2; display it after the four normal relics.
            for (var i = 0; i < 2; i++)
            {
                var entry = new MerchantRelicEntry(RelicFactory.RollRarity(player), player);
                inventory.AddRelicEntry(entry);
                entry.PurchaseCompleted += (_, _) =>
                {
                    foreach (var item in inventory.AllEntries)
                        item.OnMerchantInventoryUpdated();
                };
            }
            room.Inventories.Add(inventory);
        }
        ShopGemSync.Initialize(room, state);

        var scene = ResourceLoader.Load<PackedScene>("res://mod/Sence/Shop/MioShop1.tscn")
            ?? throw new InvalidOperationException("MioShop1.tscn could not be loaded.");
        var shop = scene.Instantiate<CustomShopOverlay>();
        shop.Initialize(room);
        var run = NRun.Instance ?? throw new InvalidOperationException("The run scene is not available.");
        run.SetCurrentRoom(shop);
        Entry.Logger.Info("[MioShop] Custom scene installed; vanilla merchant scene was not created.");
        await Hook.AfterRoomEntered(state, room);
    }
}

public sealed class CustomShopScreenContextPatch : IPatchMethod
{
    public static string PatchId => "rabbit_custom_shop_screen_context";
    public static bool IsCritical => true;
    public static string Description => "Expose the custom shop as the active screen when no overlay is open";
    public static ModPatchTarget[] GetTargets() => [new(typeof(ActiveScreenContext), "GetCurrentScreen")];

    public static void Postfix(ref IScreenContext? __result)
    {
        // Leave map, pause and other overlay screens in control of their input.
        if (__result == null && CustomShopOverlay.Instance is { } shop && shop.IsVisibleInTree())
            __result = shop;
    }
}
