using STS2RitsuLib;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models.CardPools;
using RabbitAndSteelNewMap.Scripts.Card;
using RabbitAndSteelNewMap.Scripts.Encounter;
using RabbitAndSteelNewMap.Scripts.Affliction;
using RabbitAndSteelNewMap.Scripts.Monster;
using RabbitAndSteelNewMap.Scripts.Power;

namespace RabbitAndSteelNewMap.Scripts.Act;

public static class ContentRegistration
{
    public static void Register()
    {
        RitsuLibFramework.CreateContentPack(Entry.ModId)
            .Act<KingdomOutsideAct>()
            .Act<KingdomInsideAct>()
            .Act<UnderAct>()
            .Monster<Maxi>()
            .Monster<Rem>()
            .Monster<Bella>()
            .Monster<Sohko>()
            .Monster<Ita>()
            .Monster<Pine>()
            .Monster<Mav>()
            .Monster<Jay>()
            .Monster<Lette>()
            .Monster<Blush>()
            .Monster<Avy>()
            .Monster<AvyBig>()
            .Affliction<RedColorMarkAffliction>()
            .Affliction<BlueColorMarkAffliction>()
            .Affliction<YellowColorMarkAffliction>()
            .Affliction<PurpleColorMarkAffliction>()
            .Affliction<GreenColorMarkAffliction>()
            .Power<TurbulencePower>()
            .Power<MaxiColorPower>()
            .Power<BlushColorPower>()
            .Power<RequiredColorPower>()
            .Power<PaintingPower>()
            .Power<PullAwayPower>()
            .Power<StillPower>()
            .Power<MovementPower>()
            .Power<ConstrainedPower>()
            .Power<CallPower>()
            .Power<AvyRevivalPower>()
            .Power<FrogIdolPower>()
            .Card<ColorlessCardPool, Away>()
            .ActEncounter<KingdomOutsideAct, MaxiWeak>()
            .ActEncounter<KingdomOutsideAct, RemWeak>()
            .ActEncounter<KingdomOutsideAct, BellaWeak>()
            .ActEncounter<KingdomOutsideAct, SohkoWeak>()
            .ActEncounter<KingdomOutsideAct, ItaPineWeak>()
            .ActEncounter<KingdomOutsideAct, MavStrongEncounter>()
            .ActEncounter<KingdomOutsideAct, JayLetteWeak>()
            .ActEncounter<KingdomOutsideAct, BlushElite>()
            .ActEncounter<KingdomOutsideAct, AvyBoss>()
            .ActEnterUniformPool(0)
            .ActEnterUniformPoolCandidate<KingdomOutsideAct>(0, _ => true)
            .ActEnterUniformPool(1)
            .ActEnterUniformPoolCandidate<KingdomInsideAct>(1, _ => true)
            .ActEnterUniformPool(2)
            .ActEnterUniformPoolCandidate<UnderAct>(2, _ => true)
            .Apply();
    }
}
