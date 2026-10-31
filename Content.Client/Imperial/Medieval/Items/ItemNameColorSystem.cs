using Content.Shared.CCVar;
using Content.Shared.Imperial.Medieval.Items;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Items;

public sealed class ItemNameColorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    private readonly List<StyledLabel> _labels = new();
    private sealed record StyledLabel(WeakReference<Control> Label, EntityUid Item, Color? Original);

    public static Color NameColor(ItemNameColorComponent style, double seconds, bool reducedMotion = false)
    {
        var blend = reducedMotion || !double.IsFinite(seconds) || !float.IsFinite(style.PulseSeconds) || style.PulseSeconds <= 0
            ? 0 : (float)((1 - Math.Cos(seconds * Math.PI * 2 / style.PulseSeconds)) * .5);
        return Color.InterpolateBetween(style.Color, style.PulseColor, blend);
    }

    public void Apply(Control label, EntityUid? item)
    {
        var styled = item != null && HasComp<ItemNameColorComponent>(item.Value);
        for (var i = _labels.Count - 1; i >= 0; i--)
        {
            var existing = _labels[i];
            if (!existing.Label.TryGetTarget(out var target) || target.Disposed)
            {
                _labels.RemoveAt(i);
                continue;
            }
            if (!ReferenceEquals(target, label))
                continue;
            if (styled && existing.Item == item)
                return;
            target.ModulateSelfOverride = existing.Original;
            _labels.RemoveAt(i);
        }
        if (!styled || label.Disposed)
            return;
        _labels.Add(new StyledLabel(new WeakReference<Control>(label), item!.Value, label.ModulateSelfOverride));
        label.ModulateSelfOverride = NameColor(Comp<ItemNameColorComponent>(item!.Value), _timing.RealTime.TotalSeconds, _config.GetCVar(CCVars.ReducedMotion));
    }

    public void ApplyTooltip(Control tooltip, EntityUid item)
    {
        foreach (var child in tooltip.Children)
        {
            if (child is Label or RichTextLabel)
                Apply(child, item);
            else
                ApplyTooltip(child, item);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        var seconds = _timing.RealTime.TotalSeconds;
        var reducedMotion = _config.GetCVar(CCVars.ReducedMotion);
        for (var i = _labels.Count - 1; i >= 0; i--)
        {
            var entry = _labels[i];
            if (!entry.Label.TryGetTarget(out var label) || label.Disposed)
            {
                _labels.RemoveAt(i);
                continue;
            }
            if (!Exists(entry.Item) || !HasComp<ItemNameColorComponent>(entry.Item))
            {
                label.ModulateSelfOverride = entry.Original;
                _labels.RemoveAt(i);
                continue;
            }
            label.ModulateSelfOverride = NameColor(Comp<ItemNameColorComponent>(entry.Item),
                seconds, reducedMotion);
        }
    }

    public override void Shutdown()
    {
        foreach (var entry in _labels)
            if (entry.Label.TryGetTarget(out var label) && !label.Disposed)
                label.ModulateSelfOverride = entry.Original;
        _labels.Clear();
        base.Shutdown();
    }
}
