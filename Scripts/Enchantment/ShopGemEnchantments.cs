using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Enchantment;

public enum GemKind { Attack, Skill, Power, Basic }
public enum GemColor { Blue, Green, Red, Purple, Yellow }

public abstract partial class ShopGemEnchantment : ModEnchantmentTemplate
{
    public abstract GemKind Kind { get; }
    public abstract GemColor Color { get; }
    public override bool ShowAmount => BattleLimit > 0;
    public override int DisplayAmount => Math.Max(0, BattleLimit - BattlesCompleted);
    public override bool HasExtraCardText => false;
    public string TexturePath => $"res://mod/Iamge/Shop/spr_upgrade_{Color.ToString().ToLowerInvariant()}_{TextureIndex(Kind)}.png";
    public override EnchantmentAssetProfile AssetProfile => new(IconPath: TexturePath);

    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) && card.Enchantment == null && MatchesKind(card);

    public bool CanEnchantInShop(CardModel card)
    {
        if (card.Enchantment == null)
            return CanEnchant(card);
        if (card.Enchantment is not ShopGemEnchantment old || old.Color == Color || !MatchesKind(card))
            return false;
        // Check the vanilla restrictions on an isolated copy, without mutating the real deck.
        var candidate = (CardModel)card.MutableClone();
        candidate.ClearEnchantmentInternal();
        if (card.Pile?.Type == PileType.Deck && candidate.Keywords.Contains(CardKeyword.Unplayable))
            return false;
        return CanEnchant(candidate);
    }

    private bool MatchesKind(CardModel card) => Kind switch
        {
            GemKind.Attack => card.Type == CardType.Attack,
            GemKind.Skill => card.Type == CardType.Skill,
            GemKind.Power => card.Type == CardType.Power,
            GemKind.Basic => card.Rarity == CardRarity.Basic,
            _ => false,
        };

    public static int TextureIndex(GemKind kind) => kind switch
    {
        GemKind.Attack => 0, GemKind.Basic => 1, GemKind.Power => 2, GemKind.Skill => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // Separate model IDs persist both kind and color using vanilla enchantment serialization.
    public static ShopGemEnchantment Get(GemKind kind, GemColor color) => (kind, color) switch
    {
        (GemKind.Attack, GemColor.Blue) => ModelDb.Enchantment<BlueAttackGem>(),
        (GemKind.Attack, GemColor.Green) => ModelDb.Enchantment<GreenAttackGem>(),
        (GemKind.Attack, GemColor.Red) => ModelDb.Enchantment<RedAttackGem>(),
        (GemKind.Attack, GemColor.Purple) => ModelDb.Enchantment<PurpleAttackGem>(),
        (GemKind.Attack, GemColor.Yellow) => ModelDb.Enchantment<YellowAttackGem>(),
        (GemKind.Skill, GemColor.Blue) => ModelDb.Enchantment<BlueSkillGem>(),
        (GemKind.Skill, GemColor.Green) => ModelDb.Enchantment<GreenSkillGem>(),
        (GemKind.Skill, GemColor.Red) => ModelDb.Enchantment<RedSkillGem>(),
        (GemKind.Skill, GemColor.Purple) => ModelDb.Enchantment<PurpleSkillGem>(),
        (GemKind.Skill, GemColor.Yellow) => ModelDb.Enchantment<YellowSkillGem>(),
        (GemKind.Power, GemColor.Blue) => ModelDb.Enchantment<BluePowerGem>(),
        (GemKind.Power, GemColor.Green) => ModelDb.Enchantment<GreenPowerGem>(),
        (GemKind.Power, GemColor.Red) => ModelDb.Enchantment<RedPowerGem>(),
        (GemKind.Power, GemColor.Purple) => ModelDb.Enchantment<PurplePowerGem>(),
        (GemKind.Power, GemColor.Yellow) => ModelDb.Enchantment<YellowPowerGem>(),
        (GemKind.Basic, GemColor.Blue) => ModelDb.Enchantment<BlueBasicGem>(),
        (GemKind.Basic, GemColor.Green) => ModelDb.Enchantment<GreenBasicGem>(),
        (GemKind.Basic, GemColor.Red) => ModelDb.Enchantment<RedBasicGem>(),
        (GemKind.Basic, GemColor.Purple) => ModelDb.Enchantment<PurpleBasicGem>(),
        (GemKind.Basic, GemColor.Yellow) => ModelDb.Enchantment<YellowBasicGem>(),
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };
}

public sealed class BlueAttackGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Attack; public override GemColor Color => GemColor.Blue; }
public sealed class GreenAttackGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Attack; public override GemColor Color => GemColor.Green; }
public sealed class RedAttackGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Attack; public override GemColor Color => GemColor.Red; }
public sealed class PurpleAttackGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Attack; public override GemColor Color => GemColor.Purple; }
public sealed class YellowAttackGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Attack; public override GemColor Color => GemColor.Yellow; }
public sealed class BlueSkillGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Skill; public override GemColor Color => GemColor.Blue; }
public sealed class GreenSkillGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Skill; public override GemColor Color => GemColor.Green; }
public sealed class RedSkillGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Skill; public override GemColor Color => GemColor.Red; }
public sealed class PurpleSkillGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Skill; public override GemColor Color => GemColor.Purple; }
public sealed class YellowSkillGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Skill; public override GemColor Color => GemColor.Yellow; }
public sealed class BluePowerGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Power; public override GemColor Color => GemColor.Blue; }
public sealed class GreenPowerGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Power; public override GemColor Color => GemColor.Green; }
public sealed class RedPowerGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Power; public override GemColor Color => GemColor.Red; }
public sealed class PurplePowerGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Power; public override GemColor Color => GemColor.Purple; }
public sealed class YellowPowerGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Power; public override GemColor Color => GemColor.Yellow; }
public sealed class BlueBasicGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Basic; public override GemColor Color => GemColor.Blue; }
public sealed class GreenBasicGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Basic; public override GemColor Color => GemColor.Green; }
public sealed class RedBasicGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Basic; public override GemColor Color => GemColor.Red; }
public sealed class PurpleBasicGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Basic; public override GemColor Color => GemColor.Purple; }
public sealed class YellowBasicGem : ShopGemEnchantment { public override GemKind Kind => GemKind.Basic; public override GemColor Color => GemColor.Yellow; }
