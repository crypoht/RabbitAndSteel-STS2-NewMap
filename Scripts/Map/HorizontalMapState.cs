using MegaCrit.Sts2.Core.Runs;

namespace RabbitAndSteelNewMap.Scripts.Map;

internal static class HorizontalMapState
{
    public const float EdgePadding = 80f;
    public const float HorizontalSpacingScale = 1.96f;
    public const float VerticalSpacingScale = 0.98f;

    public static float FixedY { get; set; }
    public static float CenterX { get; set; }
    public static float MinScrollX { get; set; }
    public static float MaxScrollX { get; set; }
    public static float GlobalOffsetX { get; set; }
    public static RunState? RunState { get; set; }
}
