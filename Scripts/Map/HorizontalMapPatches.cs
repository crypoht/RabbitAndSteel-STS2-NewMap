using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace RabbitAndSteelNewMap.Scripts.Map;

internal static class MapPatchReflection
{
    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    public static T? GetField<T>(object instance, string fieldName) where T : class
    {
        return instance.GetType().GetField(fieldName, InstanceNonPublic)?.GetValue(instance) as T;
    }

    public static T GetValue<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, InstanceNonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)field.GetValue(instance)!;
    }

    public static void SetValue<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, InstanceNonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    public static MethodInfo? GetMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName, InstanceNonPublic);
    }
}

public sealed class HorizontalMapLayoutPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_layout";
    public static bool IsCritical => false;
    public static string Description => "Reposition map nodes into a horizontal layout";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), "SetMap")];

    public static void Postfix(NMapScreen __instance, ActMap map)
    {
        try
        {
            var points = MapPatchReflection.GetField<Control>(__instance, "_points");
            var pointByCoord = MapPatchReflection.GetField<Dictionary<MapCoord, NMapPoint>>(__instance, "_mapPointDictionary");
            var startNode = MapPatchReflection.GetField<NMapPoint>(__instance, "_startingPointNode");
            var bossNode = MapPatchReflection.GetField<NMapPoint>(__instance, "_bossPointNode");
            var secondBossNode = MapPatchReflection.GetField<NMapPoint>(__instance, "_secondBossPointNode");
            var runState = MapPatchReflection.GetField<RunState>(__instance, "_runState");

            if (points == null || pointByCoord == null || startNode == null || bossNode == null)
                return;

            HorizontalMapState.RunState = runState;

            var rowCount = map.GetRowCount();
            var columnCount = map.GetColumnCount();
            var viewportSize = points.GetViewportRect().Size;
            var availableWidth = viewportSize.X - HorizontalMapState.EdgePadding * 2f;
            var availableHeight = viewportSize.Y - HorizontalMapState.EdgePadding * 2f;
            var xSpacing = availableWidth / (rowCount + 1f);
            var ySpacing = columnCount > 1 ? availableHeight / (columnCount - 1f) : 300f;
            var baseSpacing = Mathf.Min(xSpacing, ySpacing);
            var horizontalSpacing = baseSpacing * HorizontalMapState.HorizontalSpacingScale;
            var verticalSpacing = baseSpacing * HorizontalMapState.VerticalSpacingScale;

            var rowByNode = GetLogicalRows(pointByCoord, startNode, bossNode, secondBossNode, rowCount);
            var positionedNodes = new Dictionary<NMapPoint, Vector2>();
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var (coord, node) in pointByCoord)
            {
                var logicalRow = rowByNode[node];
                var x = logicalRow * horizontalSpacing;
                var y = (coord.col - (columnCount - 1f) / 2f) * verticalSpacing;
                var position = new Vector2(x, y);

                positionedNodes[node] = position;
                min = new Vector2(Mathf.Min(min.X, x), Mathf.Min(min.Y, y));
                max = new Vector2(Mathf.Max(max.X, x), Mathf.Max(max.Y, y));
            }

            var center = (min + max) * 0.5f;
            HorizontalMapState.GlobalOffsetX = 800f;

            foreach (var (node, position) in positionedNodes)
                node.Position = position - center + new Vector2(HorizontalMapState.GlobalOffsetX, 0f);

            RedrawPaths(__instance, pointByCoord);
            RecolorVisitedPaths(__instance, runState);
            UpdateScrollBounds(__instance, points);
            PinLegend(__instance);
            MapPatchReflection.SetValue(__instance, "_hasPlayedAnimation", true);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[HorizontalMap] Layout patch failed: {ex}");
        }
    }

    private static Dictionary<NMapPoint, float> GetLogicalRows(
        Dictionary<MapCoord, NMapPoint> pointByCoord,
        NMapPoint startNode,
        NMapPoint bossNode,
        NMapPoint? secondBossNode,
        int rowCount)
    {
        var rows = new Dictionary<NMapPoint, float>();

        foreach (var (coord, node) in pointByCoord)
        {
            if (node == startNode)
                rows[node] = -2.8f;
            else if (node == bossNode)
                rows[node] = rowCount + 0.1f;
            else if (secondBossNode != null && node == secondBossNode)
                rows[node] = rowCount + 2.6f;
            else
                rows[node] = coord.row;
        }

        return rows;
    }

    private static void RedrawPaths(NMapScreen screen, Dictionary<MapCoord, NMapPoint> pointByCoord)
    {
        var paths = MapPatchReflection.GetField<IDictionary>(screen, "_paths");
        paths?.Clear();

        var pathsContainer = MapPatchReflection.GetField<Control>(screen, "_pathsContainer");
        pathsContainer?.FreeChildren();

        var drawPaths = MapPatchReflection.GetMethod(typeof(NMapScreen), "DrawPaths");
        if (drawPaths == null)
            return;

        foreach (var node in pointByCoord.Values)
            drawPaths.Invoke(screen, [node, node.Point]);
    }

    private static void RecolorVisitedPaths(NMapScreen screen, RunState? runState)
    {
        if (runState == null)
            return;

        var paths = MapPatchReflection.GetField<Dictionary<ValueTuple<MapCoord, MapCoord>, IReadOnlyList<TextureRect>>>(screen, "_paths");
        if (paths == null)
            return;

        var visited = runState.VisitedMapCoords;
        for (var i = 0; i < visited.Count - 1; i++)
        {
            if (!paths.TryGetValue(new ValueTuple<MapCoord, MapCoord>(visited[i], visited[i + 1]), out var ticks))
                continue;

            foreach (var tick in ticks)
                tick.Modulate = runState.Act.MapTraveledColor;
        }
    }

    private static void UpdateScrollBounds(NMapScreen screen, Control points)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (var child in points.GetChildren())
        {
            if (child is not Control control)
                continue;

            min = new Vector2(Mathf.Min(min.X, control.Position.X), Mathf.Min(min.Y, control.Position.Y));
            max = new Vector2(Mathf.Max(max.X, control.Position.X + control.Size.X), Mathf.Max(max.Y, control.Position.Y + control.Size.Y));
        }

        var centerX = screen.Size.X * 0.5f;
        var centerY = screen.Size.Y * 0.5f;
        HorizontalMapState.CenterX = centerX - (min.X + max.X) * 0.5f;
        HorizontalMapState.FixedY = centerY - (min.Y + max.Y) * 0.5f;
        HorizontalMapState.MinScrollX = centerX - max.X - HorizontalMapState.EdgePadding;
        HorizontalMapState.MaxScrollX = centerX - min.X + HorizontalMapState.EdgePadding;

        var mapContainer = MapPatchReflection.GetField<Control>(screen, "_mapContainer");
        var target = new Vector2(HorizontalMapState.CenterX, HorizontalMapState.FixedY);
        if (mapContainer != null)
            mapContainer.Position = target;

        MapPatchReflection.SetValue(screen, "_targetDragPos", target);
    }

    private static void PinLegend(NMapScreen screen)
    {
        var legend = MapPatchReflection.GetField<Control>(screen, "_mapLegend");
        if (legend == null)
            return;

        legend.TopLevel = true;
        legend.Position = new Vector2(screen.Size.X - legend.Size.X + 400f, screen.Size.Y - legend.Size.Y - 550f);
    }
}

public sealed class HorizontalMapPathPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_path";
    public static bool IsCritical => false;
    public static string Description => "Draw path ticks for the horizontal map";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), "CreatePath")];

    public static bool Prefix(NMapScreen __instance, Vector2 start, Vector2 end, ref IReadOnlyList<TextureRect> __result)
    {
        var ticks = new List<TextureRect>();
        var direction = (end - start).Normalized();
        var rotation = direction.Angle() + Mathf.Pi * 0.5f;
        var distance = start.DistanceTo(end);
        var count = (int)(distance / 22f) + 1;

        for (var i = 1; i < count; i++)
        {
            var tick = PreloadManager.Cache.GetScene("res://scenes/ui/map_dot.tscn")
                .Instantiate<TextureRect>(PackedScene.GenEditState.Disabled);
            tick.Position = start + direction * (i * 22f);
            tick.Position -= new Vector2(__instance.Size.X * 0.5f - 20f, __instance.Size.Y * 0.5f - 20f);
            tick.Rotation = rotation;
            tick.FlipH = false;
            tick.Modulate = HorizontalMapState.RunState?.Act.MapUntraveledColor ?? Colors.Gray;

            MapPatchReflection.GetField<Control>(__instance, "_pathsContainer")?.AddChildSafely(tick);
            ticks.Add(tick);
        }

        __result = ticks;
        return false;
    }
}

public sealed class HorizontalMapScrollPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_scroll";
    public static bool IsCritical => false;
    public static string Description => "Clamp map scrolling horizontally";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), "UpdateScrollPosition")];

    public static void Postfix(NMapScreen __instance)
    {
        var target = MapPatchReflection.GetValue<Vector2>(__instance, "_targetDragPos");
        target.Y = HorizontalMapState.FixedY;
        target.X = Mathf.Clamp(target.X, HorizontalMapState.MinScrollX, HorizontalMapState.MaxScrollX);
        MapPatchReflection.SetValue(__instance, "_targetDragPos", target);

        var mapContainer = MapPatchReflection.GetField<Control>(__instance, "_mapContainer");
        if (mapContainer == null)
            return;

        var position = mapContainer.Position;
        position.Y = HorizontalMapState.FixedY;
        position.X = Mathf.Clamp(position.X, HorizontalMapState.MinScrollX, HorizontalMapState.MaxScrollX);
        mapContainer.Position = position;
    }
}

public sealed class HorizontalMapMousePatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_mouse";
    public static bool IsCritical => false;
    public static string Description => "Drag horizontal map with the mouse";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), "ProcessMouseEvent")];

    public static bool Prefix(NMapScreen __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseMotion motion)
            return true;

        var isDragging = MapPatchReflection.GetValue<bool>(__instance, "_isDragging");
        if (!isDragging)
            return true;

        var target = MapPatchReflection.GetValue<Vector2>(__instance, "_targetDragPos");
        target.X += motion.Relative.X;
        MapPatchReflection.SetValue(__instance, "_targetDragPos", target);
        return false;
    }
}

public sealed class HorizontalMapWheelPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_wheel";
    public static bool IsCritical => false;
    public static string Description => "Use wheel and pan gestures for horizontal map scrolling";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), "ProcessScrollEvent")];

    public static bool Prefix(NMapScreen __instance, InputEvent inputEvent)
    {
        var canScroll = MapPatchReflection.GetMethod(typeof(NMapScreen), "CanScroll");
        if (canScroll == null || !(bool)canScroll.Invoke(__instance, null)!)
            return false;

        var drag = ScrollHelper.GetDragForScrollEvent(inputEvent);
        if (drag != 0f)
        {
            var target = MapPatchReflection.GetValue<Vector2>(__instance, "_targetDragPos");
            target.X += drag;
            MapPatchReflection.SetValue(__instance, "_targetDragPos", target);

            MapPatchReflection.GetMethod(typeof(NMapScreen), "TryCancelStartOfActAnim")?.Invoke(__instance, null);
        }

        return false;
    }
}

public sealed class HorizontalMapControllerPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_controller";
    public static bool IsCritical => false;
    public static string Description => "Use controller vertical actions for horizontal map scrolling";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), "ProcessControllerEvent")];

    public static bool Prefix(NMapScreen __instance, InputEvent inputEvent)
    {
        var canScrollMethod = MapPatchReflection.GetMethod(typeof(NMapScreen), "CanScroll");
        var canScroll = canScrollMethod != null && (bool)canScrollMethod.Invoke(__instance, null)!;

        if (inputEvent.IsActionPressed(MegaInput.up, false, false) && canScroll)
        {
            ScrollBy(__instance, 400f);
            return false;
        }

        if (inputEvent.IsActionPressed(MegaInput.down, false, false) && canScroll)
        {
            ScrollBy(__instance, -400f);
            return false;
        }

        return true;
    }

    private static void ScrollBy(NMapScreen screen, float amount)
    {
        var target = MapPatchReflection.GetValue<Vector2>(screen, "_targetDragPos");
        target.X += amount;
        MapPatchReflection.SetValue(screen, "_targetDragPos", target);
        MapPatchReflection.GetMethod(typeof(NMapScreen), "TryCancelStartOfActAnim")?.Invoke(screen, null);
    }
}

public sealed class HorizontalMapBackgroundPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_background";
    public static bool IsCritical => false;
    public static string Description => "Rotate and position the map background for horizontal layout";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapBg), "OnWindowChange")];

    public static bool Prefix(NMapBg __instance)
    {
        try
        {
            __instance.RotationDegrees = -90f;
            __instance.Scale = new Vector2(1.35f, 1.35f);
            __instance.Position = new Vector2(-1227f, 1380f);

            var drawings = MapPatchReflection.GetField<NMapDrawings>(__instance, "_drawings");
            drawings?.RepositionBasedOnBackground(__instance);
            return false;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[HorizontalMap] Background patch failed: {ex}");
            return true;
        }
    }
}

public sealed class HorizontalMapDrawingsPatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_drawings";
    public static bool IsCritical => false;
    public static string Description => "Resize map drawings for horizontal background";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapDrawings), "RepositionBasedOnBackground")];

    public static bool Prefix(NMapDrawings __instance)
    {
        __instance.TopLevel = true;
        __instance.Position = Vector2.Zero;
        __instance.Size = DisplayServer.WindowGetSize();
        __instance.MouseFilter = Control.MouseFilterEnum.Ignore;
        return false;
    }
}

public sealed class HorizontalMapDrawingsStatePatch : IPatchMethod
{
    public static string PatchId => "rabbit_horizontal_map_drawings_state";
    public static bool IsCritical => false;
    public static string Description => "Resize map drawing viewport for horizontal map";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapDrawings), "GetDrawingStateForPlayer")];

    public static void Postfix(object? __result)
    {
        if (__result == null)
            return;

        try
        {
            var windowSize = DisplayServer.WindowGetSize();
            var resultType = __result.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var drawViewport = resultType.GetField("drawViewport", flags)?.GetValue(__result) as SubViewport;
            var drawingTexture = resultType.GetField("drawingTexture", flags)?.GetValue(__result) as TextureRect;

            if (drawViewport != null)
                drawViewport.Size = new Vector2I(windowSize.X / 2, windowSize.Y / 2);

            if (drawingTexture == null)
                return;

            drawingTexture.Size = windowSize;
            if (drawingTexture.GetParent() is Control parent)
                parent.Size = windowSize;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[HorizontalMap] Drawing state patch failed: {ex}");
        }
    }
}
