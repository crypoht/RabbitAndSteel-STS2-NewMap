using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Models.Capabilities;
using RabbitAndSteelNewMap.Scripts.Capability;

namespace RabbitAndSteelNewMap.Scripts.Affliction;

public enum ColorMarkType
{
    Red = 1,
    Blue = 2,
    Yellow = 3,
    Purple = 4,
    Green = 5
}

public interface IColorMarkAffliction
{
    ColorMarkType Color { get; }
}

public abstract class ColorMarkAfflictionBase : ModAfflictionTemplate, IColorMarkAffliction
{
    public abstract ColorMarkType Color { get; }

    public override bool HasExtraCardText => true;

    public override void BeforeRemoved()
    {
        Card.RemoveCapability<ColorMarkOverlayCapability>();
    }
}

[RegisterAffliction]
public sealed class RedColorMarkAffliction : ColorMarkAfflictionBase
{
    public override ColorMarkType Color => ColorMarkType.Red;
}

[RegisterAffliction]
public sealed class BlueColorMarkAffliction : ColorMarkAfflictionBase
{
    public override ColorMarkType Color => ColorMarkType.Blue;
}

[RegisterAffliction]
public sealed class YellowColorMarkAffliction : ColorMarkAfflictionBase
{
    public override ColorMarkType Color => ColorMarkType.Yellow;
}

[RegisterAffliction]
public sealed class PurpleColorMarkAffliction : ColorMarkAfflictionBase
{
    public override ColorMarkType Color => ColorMarkType.Purple;
}

[RegisterAffliction]
public sealed class GreenColorMarkAffliction : ColorMarkAfflictionBase
{
    public override ColorMarkType Color => ColorMarkType.Green;
}
