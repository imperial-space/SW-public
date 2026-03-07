using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Medieval.Ships.Anchor;

namespace Content.Server.Imperial.Medieval.Ships.Anchor;

/// <summary>
/// This handles...
/// </summary>
public sealed class ServerMedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, UseAnchorEvent>(OnUseAnchor);
    }

    private void OnUseAnchor(EntityUid uid, MedievalAnchorComponent component, UseAnchorEvent args)
    {
        if (args.Target == null || args.Cancelled)
            return;

        var target = component.Owner;

        var enabled = component.Enabled;

        ShuttleComponent? shuttleComponent = default;

        var transform = Transform(target);
        var grid = transform.GridUid;
        if (!grid.HasValue || !transform.Anchored || !Resolve(grid.Value, ref shuttleComponent))
            return;

        if (!enabled)
        {
            _shuttleSystem.Disable(grid.Value);
        }
        else
        {
            _shuttleSystem.Enable(grid.Value);
        }

        shuttleComponent.Enabled = !enabled;
        component.Enabled = !enabled;
        }
}
