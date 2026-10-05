using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RabbitAndSteelNewMap.Scripts.Enchantment;
using STS2RitsuLib.Networking.Sidecar;

namespace RabbitAndSteelNewMap.Scripts.Shop;

public static class ShopGemSync
{
    private sealed class Stock
    {
        public Dictionary<ulong, ShopGemEntry[]> Players { get; } = [];
        public HashSet<ulong> Buying { get; } = [];
    }

    public readonly record struct PurchaseMessage(int Slot, int Revision, GemColor Color, int Cost);
    private static readonly ConditionalWeakTable<MerchantRoom, Stock> Stocks = new();
    private static readonly RitsuLibSidecarSyncMessageDescriptor<PurchaseMessage> Descriptor = new(
        ModuleId: Entry.ModId,
        MessageKey: "shop_gem_purchase_v1",
        Serialize: static message => JsonSerializer.SerializeToUtf8Bytes(message),
        Deserialize: static bytes => JsonSerializer.Deserialize<PurchaseMessage>(bytes),
        Handle: Handle,
        LocationTargeted: true,
        ShouldBuffer: true,
        DispatchLocalOnBroadcast: false);

    public static void Register() => RitsuLibSidecarSyncMessages.Register(Descriptor);

    public static void Initialize(MerchantRoom room, IRunState state)
    {
        var stock = Stocks.GetOrCreateValue(room);
        foreach (var player in state.Players)
            stock.Players[player.NetId] = Enum.GetValues<GemKind>()
                .Select(kind => new ShopGemEntry(player, kind)).ToArray();
    }

    public static ShopGemEntry[] GetEntries(MerchantRoom room, Player player) =>
        Stocks.TryGetValue(room, out var stock) && stock.Players.TryGetValue(player.NetId, out var entries)
            ? entries : throw new InvalidOperationException("Gem stock was not initialized.");

    public static async Task PurchaseLocal(MerchantRoom room, ShopGemEntry entry)
    {
        var message = new PurchaseMessage((int)entry.Kind, entry.Revision, entry.Color, entry.Cost);
        // Like vanilla shop removal: peers start the same choice before the local selection resolves.
        if (!RunManager.Instance.IsSingleplayerOrFakeMultiplayer)
        {
            var result = RitsuLibSidecarSyncMessages.Send(RunManager.Instance, Descriptor, message);
            if (!result)
                throw new InvalidOperationException("Gem purchase synchronization failed.");
        }
        await Purchase(room, entry.Player.NetId, message);
    }

    private static Task Handle(RitsuLibSidecarSyncMessageContext<PurchaseMessage> context)
    {
        if (context.SenderNetId == RunManager.Instance.NetService.NetId)
            return Task.CompletedTask;
        var state = RunManager.Instance.DebugOnlyGetState();
        return state?.CurrentRoom is MerchantRoom room
            ? Purchase(room, context.SenderNetId, context.Message)
            : Task.CompletedTask;
    }

    private static async Task Purchase(MerchantRoom room, ulong ownerId, PurchaseMessage message)
    {
        if (!Stocks.TryGetValue(room, out var stock) ||
            !stock.Players.TryGetValue(ownerId, out var entries) ||
            message.Slot < 0 || message.Slot >= entries.Length || !stock.Buying.Add(ownerId))
            return;
        try
        {
            var inventory = room.Inventories.First(i => i.Player.NetId == ownerId);
            await entries[message.Slot].Purchase(message.Revision, message.Color, message.Cost, inventory);
        }
        finally
        {
            stock.Buying.Remove(ownerId);
        }
    }
}
