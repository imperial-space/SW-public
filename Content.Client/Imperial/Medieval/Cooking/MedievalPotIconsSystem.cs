using System.Numerics;
using Content.Client.Eye;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Cooking;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - keeps a <see cref="MedievalPotIcons"/> over every cooking pot the player is near,
///     feeds it what the pot holds and passes clicks on its icons to the server.
/// </summary>
public sealed class MedievalPotIconsSystem : EntitySystem
{
    private const float ShowRange = 7f;
    // a pot already shown stays a little longer, so walking along the edge does not rebuild its icons
    private const float HideRange = 8f;
    private const float HoverRadius = 0.6f;
    private const string ViewportContainer = "ViewportContainer";
    private const int HashPrime = 397;

    // boxed once: the appearance is read every frame
    private static readonly Enum StateKey = MedievalPotVisuals.State;
    private static readonly Enum ResultKey = MedievalPotVisuals.Result;
    private static readonly Enum PortionsKey = MedievalPotVisuals.Portions;
    private static readonly Enum WaterKey = MedievalPotVisuals.Water;
    private static readonly Enum MaxWaterKey = MedievalPotVisuals.MaxWater;
    private static readonly TimeSpan SightCheckInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // interface objects only, the pot's own state stays in its components
    private readonly Dictionary<EntityUid, MedievalPotIcons> _shown = new();
    private readonly LayoutContainer _root = new() { MouseFilter = Control.MouseFilterMode.Ignore };
    private Control? _screen;

    // reused every frame, never hold anything between calls
    private readonly HashSet<EntityUid> _near = new();
    private readonly List<EntityUid> _gone = new();
    private readonly List<MedievalPotIcons.Entry> _entries = new();

    public override void Initialize()
    {
        base.Initialize();

        // the icons are placed from the eye position, so it has to be final for this frame or they lag behind a moving player
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        UpdatesAfter.Add(typeof(SharedEyeSystem));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _shown.Clear();
        _root.RemoveAllChildren();
        _root.Orphan();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _near.Clear();
        AttachRoot();

        if (_player.LocalEntity is { } player && TryComp(player, out TransformComponent? playerXform))
            ShowNearPots(player, playerXform);

        RemoveFarPots();
    }

    /// <summary>
    ///     The icons live where speech bubbles do: inside the game view, under the HUD and the windows.
    ///     The screen is replaced when the player changes the HUD layout, so this is looked at every frame.
    /// </summary>
    private void AttachRoot()
    {
        var screen = _ui.ActiveScreen;
        if (screen == _screen && _root.Parent != null)
            return;

        _screen = screen;
        _root.Orphan();

        Control parent = _ui.WindowRoot;
        if (screen != null)
        {
            try
            {
                parent = screen.FindControl<LayoutContainer>(ViewportContainer);
            }
            catch (ArgumentException)
            {
                // a screen without a game view of the usual kind: the window layer will do
            }
        }

        parent.AddChild(_root);
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);
    }

    private void ShowNearPots(EntityUid player, TransformComponent playerXform)
    {
        var map = playerXform.MapID;
        var playerPos = _transform.GetWorldPosition(playerXform);
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        var canTake = _hands.TryGetEmptyHand(player, out _);
        // an eye that sees through walls sees the icons through them too
        var wallsHide = _examine.IsOccluded(player);

        var query = EntityQueryEnumerator<MedievalCookingPotComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pot, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var distance = (worldPos - playerPos).LengthSquared();
            var shown = _shown.TryGetValue(uid, out var icons);
            if (distance > (shown ? HideRange * HideRange : ShowRange * ShowRange))
                continue;

            _near.Add(uid);
            icons ??= AddIcons(uid);
            icons.Interactive = canTake;

            // not checked every frame, it is a ray cast
            if (_timing.RealTime >= icons.NextSightCheck)
            {
                icons.NextSightCheck = _timing.RealTime + SightCheckInterval;
                icons.Visible = !wallsHide || _examine.InRangeUnOccluded(player, uid, HideRange);
            }

            Place(icons, worldPos);

            var hovered = mouse.MapId == map && (mouse.Position - worldPos).LengthSquared() < HoverRadius * HoverRadius;
            // the state first: a finished dish has to send the ingredients down before they are seen gone
            SyncState(uid, icons, hovered);
            SyncContents(uid, pot, icons);
        }
    }

    private MedievalPotIcons AddIcons(EntityUid pot)
    {
        var icons = new MedievalPotIcons();
        icons.TryTake = item => RequestTake(pot, item);
        _shown.Add(pot, icons);
        _root.AddChild(icons);
        return icons;
    }

    private void Place(MedievalPotIcons icons, Vector2 worldPos)
    {
        var scale = _root.UIScale;
        var onScreen = _eye.WorldToScreen(worldPos) / scale;
        var tile = (_eye.WorldToScreen(worldPos + Vector2.UnitX) / scale - onScreen).Length();
        icons.Place(onScreen, tile);
    }

    private void SyncContents(EntityUid uid, MedievalCookingPotComponent pot, MedievalPotIcons icons)
    {
        if (!_container.TryGetContainer(uid, pot.ContainerId, out var container))
            return;

        // indexed loops: enumerating the read-only list would allocate every frame
        var items = container.ContainedEntities;
        var signature = items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            signature = unchecked(signature * HashPrime ^ items[i].GetHashCode());
        }

        if (signature == icons.Signature)
            return;

        _entries.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            // an item the server has not sent yet: leave the signature stale and try again next frame
            if (!TryComp(items[i], out MetaDataComponent? meta) || meta.EntityPrototype == null)
            {
                signature = -1;
                continue;
            }

            AddEntry(meta.EntityPrototype.ID, meta.EntityName, items[i]);
        }

        icons.Signature = signature;
        icons.SetContents(_entries);
    }

    private void AddEntry(string prototype, string name, EntityUid item)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Prototype != prototype)
                continue;

            entry.Count++;
            entry.Item = item;
            _entries[i] = entry;
            return;
        }

        _entries.Add(new MedievalPotIcons.Entry { Prototype = prototype, Name = name, Item = item, Count = 1 });
    }

    private void SyncState(EntityUid uid, MedievalPotIcons icons, bool hovered)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.TryGetData<MedievalPotState>(uid, StateKey, out var state, appearance);
        _appearance.TryGetData<string>(uid, ResultKey, out var result, appearance);
        _appearance.TryGetData<int>(uid, PortionsKey, out var portions, appearance);
        _appearance.TryGetData<int>(uid, WaterKey, out var water, appearance);
        _appearance.TryGetData<int>(uid, MaxWaterKey, out var maxWater, appearance);

        var done = state is MedievalPotState.Ready or MedievalPotState.Burnt && !string.IsNullOrEmpty(result);
        icons.SetDish(done ? result : null, portions);
        icons.SetWater(water, maxWater, hovered);
    }

    private void RemoveFarPots()
    {
        _gone.Clear();
        foreach (var uid in _shown.Keys)
        {
            if (!_near.Contains(uid))
                _gone.Add(uid);
        }

        foreach (var uid in _gone)
        {
            _shown.Remove(uid, out var icons);
            icons?.Orphan();
        }
    }

    private bool RequestTake(EntityUid pot, EntityUid item)
    {
        // the server decides; this only saves a request it would turn down
        if (_player.LocalEntity is not { } player || !_hands.TryGetEmptyHand(player, out _))
            return false;

        RaiseNetworkEvent(new MedievalPotTakeRequestEvent(GetNetEntity(pot), GetNetEntity(item)));
        return true;
    }
}
