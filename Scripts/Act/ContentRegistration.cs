using STS2RitsuLib;
using STS2RitsuLib.Scaffolding.Content;

namespace RabbitAndSteelNewMap.Scripts.Act;

public static class ContentRegistration
{
    public static void Register()
    {
        RitsuLibFramework.CreateContentPack(Entry.ModId)
            .Act<KingdomAct>()
            .Act<UnderAct>()
            .ActEnterUniformPool(1)
            .ActEnterUniformPoolCandidate<KingdomAct>(1, _ => true)
            .ActEnterUniformPool(2)
            .ActEnterUniformPoolCandidate<UnderAct>(2, _ => true)
            .Apply();
    }
}
