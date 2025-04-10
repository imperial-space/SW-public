using System.Linq;
using Content.Server.Antag;
using System.Numerics;
using Content.Server.Administration.Commands;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Antag.Components;
using Content.Shared.GameTicking.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Roles;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Server.Humanoid;
using Content.Server.RoundEnd;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server.Maps;
namespace Content.Server.GameTicking.Rules;


public sealed class PiratesRuleSystem : GameRuleSystem<PiratesRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly PricingSystem _pricingSystem = default!;
    [Dependency] private readonly NamingSystem _namingSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string GameRuleId = "Pirates";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PiratesRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<PiratesRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntSelected);
        SubscribeLocalEvent<PirateRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }
    private void OnGetBriefing(Entity<PirateRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("pirates-briefing"));
    }
    private void OnRuleLoadedGrids(Entity<PiratesRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        var query = EntityQueryEnumerator<PiratesShuttleComponent>();
        while (query.MoveNext(out var uid, out var shuttle))
        {
            if (Transform(uid).MapID == args.Map)
            {
                shuttle.AssociatedRule = ent;
                var pirates = ent.Comp;
                pirates.InitialItems.Clear();
                pirates.PirateShuttle = GetShuttle((ent, ent)) ?? EntityUid.Invalid;
                pirates.InitialShipValue = _pricingSystem.AppraiseGrid(shuttle.Owner, uid =>
                {
                    pirates.InitialItems.Add(uid);
                    return true;
                });

                break;
            }
        }
    }
    protected override void AppendRoundEndText(EntityUid uid, PiratesRuleComponent comp, GameRuleComponent gameRule, ref RoundEndTextAppendEvent ev)
    {
        var ent = comp.Owner;
        var pirates = comp;
        if (Deleted(comp.PirateShuttle))
        {
            // Total loss, the ship somehow got annihilated.
            ev.AddLine(Loc.GetString("pirates-no-ship"));
            ev.AddLine(Loc.GetString("pirates-result-totalloss"));
        }
        else
        {
            List<(double, EntityUid)> mostValuableThefts = new();
            var shuttle = comp.PirateShuttle;
            var comp1 = comp;
            var finalValue = _pricingSystem.AppraiseGrid(shuttle, uid =>
            {
                foreach (var mindId in _antag.GetAntagMinds(uid))
                {
                    if (TryComp(mindId, out MindComponent? mind) && mind.CurrentEntity == uid)
                        return false; // Don't appraise the pirates twice, we count them in separately.
                }

                return true;
            }, (uid, price) =>
            {
                if (comp1.InitialItems.Contains(uid))
                    return;

                mostValuableThefts.Add((price, uid));
                mostValuableThefts.Sort((i1, i2) => i2.Item1.CompareTo(i1.Item1));
                if (mostValuableThefts.Count > 5)
                    mostValuableThefts.Pop();
            });

            foreach (var mindId in _antag.GetAntagMinds(uid))
            {
                if (TryComp(mindId, out MindComponent? mind) && mind.CurrentEntity is not null)
                    finalValue += _pricingSystem.GetPrice(mind.CurrentEntity.Value);
            }

            var score = finalValue - pirates.InitialShipValue;
            ev.AddLine("");

            if (mostValuableThefts.Count != 0)
            {
                ev.AddLine(Loc.GetString("pirates-final-score", ("score", $"{score:F2}")));
                ev.AddLine(Loc.GetString("pirates-final-score-2", ("finalPrice", $"{finalValue:F2}")));
            }
            ev.AddLine(Loc.GetString("pirates-most-valuable"));
            foreach (var (price, obj) in mostValuableThefts)
            {
                ev.AddLine(Loc.GetString("pirates-stolen-item-entry", ("entity", obj), ("credits", $"{price:F2}")));
            }

            if (mostValuableThefts.Count == 0)
            {
                ev.AddLine(Loc.GetString("pirates-stole-nothing"));
                ev.AddLine(Loc.GetString("pirates-stole-nothing2"));
            }

            var winningscore = pirates.WinningScore;

            if (score >= winningscore)
            {
                ev.AddLine(Loc.GetString("pirates-result-victory"));
            }
            else if (mostValuableThefts.Count == 0)
            {
                ev.AddLine(Loc.GetString("pirates-result-totalloss"));
            }
            else
            {
                ev.AddLine(Loc.GetString("pirates-result-loss"));
            }
        }

        ev.AddLine("");
        ev.AddLine(Loc.GetString("pirates-list-start"));
        foreach (var pirate in _antag.GetAntagMinds(uid))
        {
            if (TryComp(pirate, out MindComponent? mind))
            {
                ev.AddLine($"- {mind.CharacterName} ({mind.Session?.Name})");
            }
        }
    }
    private void OnAfterAntagEntSelected(Entity<PiratesRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {

        _antag.SendBriefing(args.Session,
            Loc.GetString("pirate-welcome"),
            Color.Yellow,
            ent.Comp.PirateAlertSound);
    }
    private EntityUid? GetShuttle(Entity<PiratesRuleComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return null;

        var query = EntityQueryEnumerator<PiratesShuttleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.AssociatedRule == ent.Owner)
                return uid;
        }

        return null;
    }
}

