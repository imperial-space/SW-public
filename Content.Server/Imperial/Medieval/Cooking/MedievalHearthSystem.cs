using Content.Server.Popups;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Cooking;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Medieval.Cooking;

public sealed class MedievalHearthSystem : EntitySystem
{
    private const float TickInterval = 1f;

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalHearthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalHearthComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MedievalHearthComponent, ExaminedEvent>(OnExamined);
    }

    public bool IsLit(MedievalHearthComponent hearth)
    {
        return hearth.State != MedievalHearthState.Out;
    }

    private void OnStartup(EntityUid uid, MedievalHearthComponent hearth, ComponentStartup args)
    {
        // a hearth placed with fuel in its prototype starts lit
        UpdateState(uid, hearth, true);
    }

    private void OnInteractUsing(EntityUid uid, MedievalHearthComponent hearth, InteractUsingEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(args.Used) || EntityManager.IsQueuedForDeletion(args.Used)
            || !TryGetFuel(hearth, args.Used, out var seconds))
            return;

        args.Handled = true;

        if (hearth.BurnTime + seconds > hearth.LogSlots * hearth.SlotBurnTime)
        {
            _popup.PopupEntity(Loc.GetString("medieval-hearth-full"), uid, args.User);
            return;
        }

        if (!ConsumeFuel(args.Used))
            return;

        var wasOut = hearth.State == MedievalHearthState.Out;
        hearth.BurnTime += seconds;
        UpdateState(uid, hearth);

        _popup.PopupEntity(Loc.GetString(wasOut ? "medieval-hearth-lit" : "medieval-hearth-fed"), uid, args.User);
        _audio.PlayPvs(hearth.FuelSound, uid);
    }

    private void OnExamined(EntityUid uid, MedievalHearthComponent hearth, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var key = hearth.State switch
        {
            MedievalHearthState.Burning => "medieval-hearth-examine-burning",
            MedievalHearthState.Low => "medieval-hearth-examine-low",
            MedievalHearthState.Dying => "medieval-hearth-examine-dying",
            _ => "medieval-hearth-examine-out"
        };

        args.PushMarkup($"[color={GetExamineColor(hearth).ToHex()}]{Loc.GetString(key)}[/color]");
    }

    /// <summary>
    ///     A bigger pile burns hotter: the text goes from orange to red with every log added.
    /// </summary>
    private static Color GetExamineColor(MedievalHearthComponent hearth)
    {
        switch (hearth.State)
        {
            case MedievalHearthState.Out:
                return Color.FromHex("#8a8178");
            case MedievalHearthState.Dying:
                return Color.FromHex("#c9a458");
            case MedievalHearthState.Low:
                return Color.FromHex("#e0b24a");
        }

        var heat = (GetLogCount(hearth) - 2) / (float) Math.Max(1, hearth.LogSlots - 2);
        return Color.InterpolateBetween(Color.FromHex("#e8923a"), Color.FromHex("#d8402a"), Math.Clamp(heat, 0f, 1f));
    }

    private bool TryGetFuel(MedievalHearthComponent hearth, EntityUid used, out float seconds)
    {
        seconds = 0f;

        if (TryComp<StackComponent>(used, out var stack))
            return hearth.StackFuels.TryGetValue(stack.StackTypeId, out seconds);

        var prototype = MetaData(used).EntityPrototype;
        return prototype != null && hearth.Fuels.TryGetValue(prototype.ID, out seconds);
    }

    private bool ConsumeFuel(EntityUid used)
    {
        if (TryComp<StackComponent>(used, out var stack))
            return _stack.Use(used, 1, stack);

        QueueDel(used);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalHearthComponent>();
        while (query.MoveNext(out var uid, out var hearth))
        {
            if (hearth.BurnTime <= 0f)
                continue;

            hearth.TickTimer += frameTime;
            if (hearth.TickTimer < TickInterval)
                continue;

            hearth.BurnTime = MathF.Max(0f, hearth.BurnTime - hearth.TickTimer);
            hearth.TickTimer = 0f;
            UpdateState(uid, hearth);
        }
    }

    private static int GetLogCount(MedievalHearthComponent hearth)
    {
        if (hearth.BurnTime <= 0f)
            return 0;

        return Math.Min(hearth.LogSlots, (int) MathF.Ceiling(hearth.BurnTime / Math.Max(1f, hearth.SlotBurnTime)));
    }

    private static MedievalHearthState GetState(MedievalHearthComponent hearth)
    {
        var logs = GetLogCount(hearth);
        if (logs == 0)
            return MedievalHearthState.Out;

        if (logs > 1)
            return MedievalHearthState.Burning;

        return hearth.BurnTime <= hearth.DyingBurnTime ? MedievalHearthState.Dying : MedievalHearthState.Low;
    }

    private void UpdateState(EntityUid uid, MedievalHearthComponent hearth, bool force = false)
    {
        var state = GetState(hearth);
        var logs = GetLogCount(hearth);
        if (state == hearth.State && logs == hearth.VisualLogs && !force)
            return;

        hearth.VisualLogs = logs;
        hearth.State = state;
        var lit = state != MedievalHearthState.Out;

        _appearance.SetData(uid, MedievalHearthVisuals.State, state);
        _appearance.SetData(uid, MedievalHearthVisuals.Logs, logs);
        _lights.SetEnabled(uid, lit);
        _ambient.SetAmbience(uid, lit);
    }
}
