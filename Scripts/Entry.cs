using System.Reflection;
using Godot.Bridge;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Logging;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Patching.Core;
using RabbitAndSteelNewMap.Scripts.Act;
using RabbitAndSteelNewMap.Scripts.Map;

namespace RabbitAndSteelNewMap.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "rabbit_and_steel_new_map";
    public static Logger Logger { get; private set; } = null!;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Logger = RitsuLibFramework.CreateLogger(ModId);
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        CustomMapNodeRegistry.Initialize();
        ApplyPatches();
        ContentRegistration.Register();
    }

    private static void ApplyPatches()
    {
        var patcher = RitsuLibFramework.CreatePatcher(ModId, "map-ui");
        patcher.RegisterPatches<MapUiPatches>();

        if (!patcher.PatchAll())
            throw new InvalidOperationException("Failed to apply map UI patches.");
    }
}
