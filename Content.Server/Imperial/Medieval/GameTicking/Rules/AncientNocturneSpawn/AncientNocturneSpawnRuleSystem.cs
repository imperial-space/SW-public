using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Humanoid;
using Content.Server.Nocturn;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mind;
using Content.Shared.Nocturn.Components;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class AncientNocturneSpawnRuleSystem : GameRuleSystem<AncientNocturneSpawnRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AncientNocturneComponent, PlayerAttachedEvent>(OnAncientNocturneAttached);
        SubscribeLocalEvent<HellfireInquisitionMemberComponent, PlayerAttachedEvent>(OnInquisitorAttached);
    }

    protected override void Started(
        EntityUid uid,
        AncientNocturneSpawnRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var markers = EntityManager.AllEntities<AncientNocturneSpawnMarkerComponent>()
            .Where(marker => !marker.Comp.Used)
            .ToList();
        _random.Shuffle(markers);

        var spawnCount = Math.Min(Math.Max(component.SpawnCount, 0), markers.Count);
        for (var i = 0; i < spawnCount; i++)
        {
            var marker = markers[i];
            Spawn(component.SpawnerPrototype, Transform(marker.Owner).Coordinates);
            marker.Comp.Used = true;
        }

        if (spawnCount == 0)
        {
            Log.Error("Ancient nocturne spawn rule started without available spawn markers");
        }
        else
        {
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("medieval-ancient-nocturne-event-announcement", ("count", spawnCount)),
                playSound: true,
                colorOverride: Color.MediumPurple,
                sender: Loc.GetString("medieval-ancient-nocturne-event-sender"));

            StartInquisitionTimer(uid, component);
        }

        GameTicker.EndGameRule(uid, gameRule);
    }

    private void OnAncientNocturneAttached(
        EntityUid uid,
        AncientNocturneComponent component,
        PlayerAttachedEvent args)
    {
        if (component.ProfileApplied)
            return;

        if (!_preferences.TryGetCachedPreferences(args.Player.UserId, out var preferences))
        {
            Log.Error($"Ancient nocturne ghost role taken without cached preferences for {args.Player.UserId}");
            return;
        }

        var profiles = preferences.Characters.Values
            .OfType<HumanoidCharacterProfile>()
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .ToList();

        if (profiles.Count == 0)
        {
            Log.Error($"Ancient nocturne ghost role taken without character profiles for {args.Player.UserId}");
            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            Log.Error($"Ancient nocturne ghost role spawned without humanoid appearance for {args.Player.UserId}");
            return;
        }

        var profile = _random.Pick(profiles);
        _metaData.SetEntityName(uid, profile.Name);
        _humanoidAppearance.SetSex(uid, profile.Sex, false, humanoid);
        _humanoidAppearance.SetGender((uid, humanoid), profile.Gender);
        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Hair);
        _humanoidAppearance.AddMarking(
            uid,
            profile.Appearance.HairStyleId,
            profile.Appearance.HairColor,
            humanoid: humanoid);
        component.ProfileApplied = true;
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        AncientNocturneSpawnRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var ruleQuery = EntityQueryEnumerator<AncientNocturneSpawnRuleComponent>();
        while (ruleQuery.MoveNext(out var ruleUid, out _))
        {
            if (ruleUid.Id < uid.Id)
                return;
        }

        var nocturneMinds = new List<Entity<MindComponent>>();
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindUid, out var mind))
        {
            if (_role.MindGetAllRoleInfo(mindUid)
                .Any(role => role.Antagonist && role.Prototype == component.AntagPrototype.Id))
                nocturneMinds.Add((mindUid, mind));
        }

        if (nocturneMinds.Count == 0)
            return;

        args.AddLine(Loc.GetString(
            "medieval-ancient-nocturne-round-end-summary",
            ("count", nocturneMinds.Count)));

        foreach (var mind in nocturneMinds)
        {
            var owner = GetEntity(mind.Comp.OriginalOwnedEntity);
            var name = mind.Comp.CharacterName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = owner is { } ownerUid
                    ? Name(ownerUid)
                    : Loc.GetString("medieval-ancient-nocturne-round-end-unknown-name");
            }

            if (owner is not { } bloodRubyOwner ||
                !TryComp<BloodRubyOwnerComponent>(bloodRubyOwner, out var ownerComponent) ||
                ownerComponent.BloodRuby is not { } ruby ||
                TerminatingOrDeleted(ruby) ||
                !TryComp<BloodRubyComponent>(ruby, out var rubyComponent))
            {
                args.AddLine(Loc.GetString(
                    "medieval-ancient-nocturne-round-end-ruby-lost",
                    ("name", name)));
                continue;
            }

            args.AddLine(Loc.GetString(
                "medieval-ancient-nocturne-round-end-blood-collected",
                ("name", name),
                ("amount", (int) MathF.Round(rubyComponent.TotalBlood))));
        }
    }

    private void OnInquisitorAttached(
        EntityUid uid,
        HellfireInquisitionMemberComponent component,
        PlayerAttachedEvent args)
    {
        if (component.ProfileApplied)
            return;

        if (!_preferences.TryGetCachedPreferences(args.Player.UserId, out var preferences))
        {
            Log.Error($"Hellfire inquisitor ghost role taken without cached preferences for {args.Player.UserId}");
            return;
        }

        var profiles = preferences.Characters.Values
            .OfType<HumanoidCharacterProfile>()
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .ToList();

        if (profiles.Count == 0)
        {
            Log.Error($"Hellfire inquisitor ghost role taken without character profiles for {args.Player.UserId}");
            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            Log.Error($"Hellfire inquisitor ghost role spawned without humanoid appearance for {args.Player.UserId}");
            return;
        }

        var profile = _random.Pick(profiles).WithSpecies("Human");
        _metaData.SetEntityName(uid, profile.Name);
        _humanoidAppearance.LoadProfile(uid, profile, humanoid);
        component.ProfileApplied = true;
    }

    private void StartInquisitionTimer(EntityUid uid, AncientNocturneSpawnRuleComponent component)
    {
        var request = new InquisitionSpawnRequest(
            component.InquisitionLeaderSpawnerPrototype,
            component.InquisitionKnightSpawnerPrototype,
            component.InquisitionChaplainSpawnerPrototype,
            Math.Max(component.InquisitionKnightCount, 0),
            Math.Max(component.InquisitionSpawnOffset, 0f));

        var delay = component.InquisitionDelay < TimeSpan.Zero
            ? TimeSpan.Zero
            : component.InquisitionDelay;
        _ = RunInquisitionTimer(uid, request, delay);
    }

    private async Task RunInquisitionTimer(EntityUid uid, InquisitionSpawnRequest request, TimeSpan delay)
    {
        await Robust.Shared.Timing.Timer.Delay(delay);

        if (TerminatingOrDeleted(uid) || !TryComp<AncientNocturneSpawnRuleComponent>(uid, out _))
            return;

        SpawnInquisition(request);
    }

    private void SpawnInquisition(InquisitionSpawnRequest request)
    {
        var markers = EntityManager.AllEntities<HellfireInquisitionSpawnMarkerComponent>()
            .Where(marker => !marker.Comp.Used)
            .ToList();

        if (markers.Count == 0)
        {
            Log.Error("Hellfire Inquisition timer completed without available spawn markers");
            return;
        }

        var marker = _random.Pick(markers);
        marker.Comp.Used = true;

        var spawners = new List<EntProtoId>
        {
            request.LeaderSpawnerPrototype,
            request.ChaplainSpawnerPrototype
        };

        for (var i = 0; i < request.KnightCount; i++)
            spawners.Add(request.KnightSpawnerPrototype);

        var coordinates = Transform(marker.Owner).Coordinates;
        var angleOffset = _random.NextFloat(0f, MathF.Tau);
        for (var i = 0; i < spawners.Count; i++)
        {
            var angle = angleOffset + MathF.Tau * i / spawners.Count;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * request.SpawnOffset;
            Spawn(spawners[i], coordinates.Offset(offset));
        }
    }

    private readonly record struct InquisitionSpawnRequest(
        EntProtoId LeaderSpawnerPrototype,
        EntProtoId KnightSpawnerPrototype,
        EntProtoId ChaplainSpawnerPrototype,
        int KnightCount,
        float SpawnOffset);
}
