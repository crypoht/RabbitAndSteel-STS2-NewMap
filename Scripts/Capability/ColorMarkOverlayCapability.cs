using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using RabbitAndSteelNewMap.Scripts.Affliction;

namespace RabbitAndSteelNewMap.Scripts.Capability;

[RegisterModelCapability]
public sealed class ColorMarkOverlayCapability :
    CardCapability,
    ICardOverlayContributor,
    ICardOverlayAssetPathContributor
{
    private static readonly string[] OverlayScenePaths =
    [
        "res://mod/Affliction/Overlay/red_color_mark_affliction.tscn",
        "res://mod/Affliction/Overlay/blue_color_mark_affliction.tscn",
        "res://mod/Affliction/Overlay/yellow_color_mark_affliction.tscn",
        "res://mod/Affliction/Overlay/purple_color_mark_affliction.tscn",
        "res://mod/Affliction/Overlay/green_color_mark_affliction.tscn"
    ];

    private ColorMarkType _color;

    public void SetColor(ColorMarkType color)
    {
        Modify(_ => _color = color);
    }

    public IEnumerable<CardOverlayContribution> GetCardOverlays(CardOverlayContext context)
    {
        if (!Enum.IsDefined(_color))
            return Array.Empty<CardOverlayContribution>();

        return
        [
            CardOverlayContribution.FromScenePath(
                "color-mark",
                GetOverlayScenePath(_color),
                order: 100,
                fullRect: false)
        ];
    }

    public IEnumerable<string> GetCardOverlayAssetPaths(CardModel card)
    {
        return OverlayScenePaths;
    }

    protected override JsonNode? SaveAdditionalState()
    {
        return JsonValue.Create((int)_color);
    }

    protected override void LoadAdditionalState(JsonNode? state, int schemaVersion)
    {
        if (state is JsonValue value
            && value.TryGetValue<int>(out var color)
            && Enum.IsDefined((ColorMarkType)color))
        {
            _color = (ColorMarkType)color;
        }
    }

    private static string GetOverlayScenePath(ColorMarkType color)
    {
        return color switch
        {
            ColorMarkType.Red => "res://mod/Affliction/Overlay/red_color_mark_affliction.tscn",
            ColorMarkType.Blue => "res://mod/Affliction/Overlay/blue_color_mark_affliction.tscn",
            ColorMarkType.Yellow => "res://mod/Affliction/Overlay/yellow_color_mark_affliction.tscn",
            ColorMarkType.Purple => "res://mod/Affliction/Overlay/purple_color_mark_affliction.tscn",
            ColorMarkType.Green => "res://mod/Affliction/Overlay/green_color_mark_affliction.tscn",
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
        };
    }
}
