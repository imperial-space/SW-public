using Content.Server.Chat.Systems;
using Content.Shared.Imperial.Medieval.LastWords;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.LastWords;

public sealed class LastWordsSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, string> _store = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<LastWordsComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<LastWordsComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _store.Clear());
    }

    private void OnEntitySpoke(Entity<LastWordsComponent> ent, ref EntitySpokeEvent args)
    {
        if (!HasComp<ActorComponent>(ent) || !HasComp<HumanoidAppearanceComponent>(ent))
            return;

        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        ent.Comp.LastWords = args.Message;
    }

    private void OnMobStateChanged(Entity<LastWordsComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.NewMobState == args.OldMobState)
            return;

        if (string.IsNullOrWhiteSpace(ent.Comp.LastWords))
            return;

        Record(ent.Owner, ent.Comp.LastWords);
    }

    public void Record(EntityUid uid, string words)
    {
        _store[uid] = words;
    }
 
    public IReadOnlyCollection<string> GetAll() => _store.Values;
}
