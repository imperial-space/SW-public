using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage;
using Content.Shared.Camera;
using System.Numerics;
using Robust.Server.Audio;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossSummoningTriggerSystem : EntitySystem
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _camera = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossSummoningTriggerComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<BossSummoningTriggerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BossSummoningTriggerComponent, BossSummonDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BossSummoningTriggerComponent, DoAfterAttemptEvent<BossSummonDoAfterEvent>>(OnAttempt);
        SubscribeLocalEvent<BossSummoningTriggerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BossSummoningTriggerComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<BossSummoningTriggerComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnInteract(EntityUid uid, BossSummoningTriggerComponent comp, InteractHandEvent args)
    {
        if (!args.Handled) args.Handled = TryActivate(uid, args.User);
    }

    private void OnActivate(EntityUid uid, BossSummoningTriggerComponent comp, ActivateInWorldEvent args)
    {
        if (!args.Handled) args.Handled = TryActivate(uid, args.User);
    }

    public bool TryActivate(EntityUid crystal, EntityUid user)
    {
        if (!TryComp<BossSummoningTriggerComponent>(crystal, out var comp) || comp.BreakToSummon) return false;
        if (comp.Activating && _doAfter.IsRunning(comp.Cast))
        {
            if (comp.Cast!.Value.Uid != user) return false;
            _doAfter.Cancel(comp.Cast);
            return true;
        }
        comp.Activating = false;
        comp.Cast = null;
        if (!CanActivate(crystal, user, out var arena)) return false;
        var duration = comp.CastDuration;
        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, duration,
                new BossSummonDoAfterEvent(), crystal, target: crystal)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DuplicateCondition = DuplicateConditions.SameEvent,
            BlockDuplicate = true,
            CancelDuplicate = true
        }, out var id)) return false;
        comp.Cast = id;
        comp.Activating = true;
        return true;
    }

    private bool CanActivate(EntityUid crystal, EntityUid user, out Entity<BossArenaMapComponent> arena, bool requireRange = true)
    {
        arena = default;
        if (!Exists(crystal) || Terminating(crystal) || !Exists(user) || Terminating(user) ||
            !_mobs.IsAlive(user) || HasComp<GhostComponent>(user) ||
            HasComp<FightingBossComponent>(user) || HasComp<BossLegacyParticipantComponent>(user) ||
            Transform(crystal).MapUid is not { } map || Transform(user).MapUid != map ||
            !TryComp<BossArenaMapComponent>(map, out var component) || component.BossPrototype == null ||
            component.Crystal != crystal || component.VictoryDeadline != null || Exists(component.Boss) ||
            requireRange && !_interaction.InRangeUnobstructed(user, crystal))
            return false;
        arena = new(map, component);
        return true;
    }

    private void OnBeforeDamage(EntityUid uid, BossSummoningTriggerComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (!comp.BreakToSummon || !args.Damage.AnyPositive()) return;
        if (comp.Activating || !float.IsFinite(comp.Health) || comp.Health <= 0 ||
            _rules.ResolveSource(args.Origin) is not { } source ||
            !CanActivate(uid, source.Player, out _, requireRange: false)) args.Cancelled = true;
    }

    private void OnDamage(EntityUid uid, BossSummoningTriggerComponent comp, DamageChangedEvent args)
    {
        if (!comp.BreakToSummon || comp.Activating) return;
        _appearance.SetData(uid, BossCrystalVisuals.Cracked,
            args.Damageable.TotalDamage.Float() >= comp.Health * comp.CrackedFraction);
        if (!args.DamageIncreased || _rules.ResolveSource(args.Origin) is not { } source ||
            !CanActivate(uid, source.Player, out var arena, requireRange: false)) return;
        if (args.Damageable.TotalDamage.Float() >= comp.Health)                  // summon when the crystal takes enough damage
            Summon(uid, source.Player, comp, arena);
        else
            _audio.PlayPvs(comp.HitSound, Transform(uid).Coordinates);
    }

    private void OnAttempt(EntityUid uid, BossSummoningTriggerComponent comp, DoAfterAttemptEvent<BossSummonDoAfterEvent> args)
    {
        if (!CanActivate(uid, args.DoAfter.Args.User, out _)) args.Cancel();
    }

    private void OnDoAfter(EntityUid uid, BossSummoningTriggerComponent comp, BossSummonDoAfterEvent args)
    {
        if (args.Handled || comp.Cast != args.DoAfter.Id) return;
        comp.Cast = null;
        comp.Activating = false;
        args.Handled = true;
        if (args.Cancelled || !CanActivate(uid, args.User, out var arena)) return;
        Summon(uid, args.User, comp, arena);
    }

    private void Summon(EntityUid uid, EntityUid user, BossSummoningTriggerComponent comp, Entity<BossArenaMapComponent> arena)
    {
        comp.Activating = true;                                                 // don't let a second hit summon another boss
        var boss = Spawn(arena.Comp.BossPrototype, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        arena.Comp.Boss = boss;
        if (!_telegraphs.TryGetPlayerTile(boss, user, out _) ||
            !EntityManager.System<BossSystem>().TryStartBossfight(_telegraphs.LivingPlayers(boss), boss))
        {
            arena.Comp.Boss = EntityUid.Invalid;
            Del(boss);                                                          // start failed, leave the crystal so they can try again
            comp.Activating = false;
            return;
        }
        EntityManager.System<BossFocusMarkerSystem>().SetTank(boss, user);       // the summoner becomes the first tank
        _audio.PlayPvs(comp.BreakSound, Transform(uid).Coordinates);
        if (comp.SpawnEffect is { } effect) Spawn(effect, Transform(boss).Coordinates);
        foreach (var additional in comp.AdditionalSpawnEffects) Spawn(additional, Transform(boss).Coordinates);
        if (comp.SpawnCameraKick > 0 && float.IsFinite(comp.SpawnCameraKick))
            foreach (var player in _telegraphs.LivingPlayers(boss))
                _camera.KickCamera(player, new Vector2(comp.SpawnCameraKick, comp.SpawnCameraKick));
        arena.Comp.Crystal = null;
        QueueDel(uid);
    }

    private void OnShutdown(EntityUid uid, BossSummoningTriggerComponent comp, ComponentShutdown args)
    {
        var cast = comp.Cast;
        comp.Cast = null;
        comp.Activating = false;
        if (_doAfter.IsRunning(cast)) _doAfter.Cancel(cast);
    }

    public void Restore(Entity<BossArenaMapComponent> arena)
    {
        arena.Comp.Boss = EntityUid.Invalid;
        if (arena.Comp.Crystal is { } crystal && Exists(crystal) && !Terminating(crystal)) return; // keep an existing crystal instead of duplicating it
        var coordinates = new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f);
        if (arena.Comp.SummoningPrototype is { } trigger)
            arena.Comp.Crystal = Spawn(trigger, coordinates);
        else if (arena.Comp.BossPrototype is { } boss)
            arena.Comp.Boss = Spawn(boss, coordinates);
    }
}
