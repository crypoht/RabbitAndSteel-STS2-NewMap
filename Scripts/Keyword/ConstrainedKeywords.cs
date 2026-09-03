using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace RabbitAndSteelNewMap.Scripts.Keyword;

[RegisterOwnedCardKeyword(
    nameof(Constrained),
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
public sealed class ConstrainedKeywords
{
    public static readonly CardKeyword Constrained =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Constrained))
            .GetModCardKeyword();
}
