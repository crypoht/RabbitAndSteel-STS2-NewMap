using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Enchantment;

public sealed class ShopGemCostPatch : IPatchMethod
{
    public static string PatchId => "shop_gem_base_energy_cost";
    public static bool IsCritical => true;
    public static string Description => "Apply gem costs before temporary free-card modifiers";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardEnergyCost), "GetWithModifiers")];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var baseField = AccessTools.Field(typeof(CardEnergyCost), "_base");
        var cardField = AccessTools.Field(typeof(CardEnergyCost), "_card");
        var patched = false;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            // Only the first read initializes the calculated cost. Leave sentinel checks intact.
            if (!patched && instruction.LoadsField(baseField))
            {
                patched = true;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, cardField);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ShopGemCostPatch), nameof(Adjust)));
            }
        }
        if (!patched)
            throw new InvalidOperationException("CardEnergyCost base-cost read was not found.");
    }

    public static int Adjust(int original, CardEnergyCost energy, CardModel card, CostModifiers modifiers)
    {
        if (card.IsCanonical || original < 0 || energy.CostsX || !modifiers.HasFlag(CostModifiers.Local))
            return original;
        var adjustment = (card.Enchantment as ShopGemEnchantment)?.CostAdjustment ?? 0;
        return Math.Max(0, original + adjustment);
    }

    public static void Postfix(CardEnergyCost __instance, CardModel ____card,
        CostModifiers modifiers, ref int __result)
    {
        if (____card.IsCanonical || __result < 0 || __instance.CostsX ||
            !modifiers.HasFlag(CostModifiers.Global) || ____card.Pile?.Type != PileType.Hand)
            return;
        var reduction = PileType.Hand.GetPile(____card.Owner).Cards.Count(c =>
            c != ____card && c.Enchantment is ShopGemEnchantment { Kind: GemKind.Basic, Color: GemColor.Red });
        __result = Math.Max(0, __result - reduction);
    }
}

public sealed class ShopGemKeywordPatch : IPatchMethod
{
    public static string PatchId => "shop_gem_derived_keywords";
    public static bool IsCritical => true;
    public static string Description => "Derive gem keywords without persisting them onto cloned cards";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardModel), "GetKeywordsWithSources")];

    public static void Postfix(CardModel __instance, KeywordSources sources, ref IReadOnlySet<CardKeyword> __result)
    {
        if (!sources.HasFlag(KeywordSources.Global) ||
            __instance.Enchantment is not ShopGemEnchantment { GrantedKeyword: { } keyword })
            return;
        var result = new HashSet<CardKeyword>(__result) { keyword };
        __result = result;
    }
}

public sealed class ShopGemDescriptionPatch : IPatchMethod
{
    public static string PatchId => "shop_gem_dynamic_descriptions";
    public static bool IsCritical => true;
    public static string Description => "Refresh remaining battles and same-name counts before displaying gem text";
    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(EnchantmentModel), "get_DynamicDescription"),
        new(typeof(EnchantmentModel), "get_DynamicExtraCardText"),
    ];

    public static void Prefix(EnchantmentModel __instance)
    {
        if (__instance is ShopGemEnchantment gem)
            gem.RefreshDescriptionValues();
    }
}
