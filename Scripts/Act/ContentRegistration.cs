using STS2RitsuLib;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Act;

public static class ContentRegistration
{
    public static void Register()
    {
        RitsuLibFramework.CreateContentPack(Entry.ModId)
            .Act<KingdomOutsideAct>()
            .Act<KingdomInsideAct>()
            .Act<UnderAct>()
            .ActEnterUniformPool(0)
            .ActEnterUniformPoolCandidate<KingdomOutsideAct>(0, _ => true)
            .ActEnterUniformPool(1)
            .ActEnterUniformPoolCandidate<KingdomInsideAct>(1, _ => true)
            .ActEnterUniformPool(2)
            .ActEnterUniformPoolCandidate<UnderAct>(2, _ => true)
            .Apply();
    }
}
