using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.BadSmell.Components;
using System.Linq;
using Content.Server.SpikeTrap.Components;
using Content.Shared.Nocturn.Components;
using Content.Server.MedievalFactionFlag.Components;
using Content.Server.Imperial.DayTime;
using Content.Shared.Damage;
using Content.Shared.Speech;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class RoundStatCounterRuleSystem : GameRuleSystem<RoundStatCounterRuleComponent>
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DayTimeSystem _dayTime = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AffectRoundStatsComponent, ScreamActionEvent>(OnScreamAction);
        SubscribeLocalEvent<AffectRoundStatsComponent, MobStateChangedEvent>(OnDeath);
        SubscribeLocalEvent<AffectRoundStatsComponent, BeforeDamageChangedEvent>(OnDamage);
    }

    private void OnScreamAction(EntityUid uid, AffectRoundStatsComponent comp, ScreamActionEvent args)
    {
        if (args.Handled)
            return;

        comp.Screams++;
        foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
        {
            barrier.Screams++;
        }
    }

    private void OnDeath(EntityUid uid, AffectRoundStatsComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
        {
            barrier.HumanDeath++;
            if (barrier.FirstDeath == "nobody")
                barrier.FirstDeath = Name(uid);
        }
    }

    private void OnDamage(EntityUid uid, AffectRoundStatsComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat) && HasComp<MedievalZveresHeatComponent>(uid) && heat > 0)
        {
            foreach (var barrierheat in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                barrierheat.ZveresHeat += heat.Float();
        }
        if (args.Damage.GetTotal() < 4)
            return;
        comp.HitCount++;
        foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
            barrier.HumanHurt++;
    }

    protected override void AppendRoundEndText(EntityUid uid,
        RoundStatCounterRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);
        var nobody = Loc.GetString("medieval-hm-barrierpoint-nobody");
        int punchingBagHits = 0;
        string punchingBagName = nobody;
        int screamerScreams = 0;
        string screamerName = nobody;
        int potionsCount = 0;
        string potionsName = nobody;
        int lockpickCount = 0;
        string lockpickerName = nobody;
        int craftsCount = 0;
        string crafterName = nobody;
        int diggsCount = 0;
        string diggerName = nobody;
        int alcoholDrinks = 0;
        string alcoholickName = nobody;

        var targets = EntityManager.AllEntities<AffectRoundStatsComponent>();
        if (targets.Count() > 0)
        {
            potionsCount = targets.MaxBy(x => x.Comp.Potions).Comp.Potions;
            potionsName = Name(targets.MaxBy(x => x.Comp.Potions).Owner);

            lockpickCount = targets.MaxBy(x => x.Comp.Lockpicks).Comp.Lockpicks;
            potionsName = Name(targets.MaxBy(x => x.Comp.Lockpicks).Owner);

            diggsCount = targets.MaxBy(x => x.Comp.Diggs).Comp.Diggs;
            diggerName = Name(targets.MaxBy(x => x.Comp.Diggs).Owner);

            alcoholDrinks = targets.MaxBy(x => x.Comp.Alcohol / 2).Comp.Alcohol / 2;
            alcoholickName = Name(targets.MaxBy(x => x.Comp.Alcohol / 2).Owner);

            craftsCount = targets.MaxBy(x => x.Comp.Crafts).Comp.Crafts;
            crafterName = Name(targets.MaxBy(x => x.Comp.Crafts).Owner);

            screamerScreams = targets.MaxBy(x => x.Comp.Screams).Comp.Screams;
            screamerName = Name(targets.MaxBy(x => x.Comp.Screams).Owner);

            punchingBagHits = targets.MaxBy(x => x.Comp.HitCount).Comp.HitCount;
            punchingBagName = Name(targets.MaxBy(x => x.Comp.HitCount).Owner);
        }

        foreach (var (target, comp) in targets)
        {
            if (!_mobState.IsAlive(target))
                continue;

            var xform = Transform(target);
            var coords = xform.Coordinates;

            Spawn("MedievalSpawnerDarkSkeletonRandom", coords);
        }

        int worstSmell = 0;
        string worstSmellName = nobody;
        int bestSmell = 0;
        string bestSmellName = nobody;

        var smell = EntityManager.AllEntities<BadSmellComponent>();
        if (smell.Count() > 0)
        {
            worstSmell = smell.MaxBy(x => x.Comp.WorstSmell).Comp.WorstSmell;
            worstSmellName = Name(smell.MaxBy(x => x.Comp.WorstSmell).Owner);

            bestSmell = smell.MaxBy(x => x.Comp.BestSmell).Comp.BestSmell;
            bestSmellName = Name(smell.MaxBy(x => x.Comp.BestSmell).Owner);
        }

        _dayTime.ChangePreset("0", "bloody", true);

        int legion = 0;
        int insurgency = 0;
        foreach (var flag in EntityManager.EntityQuery<MedievalFactionFlagComponent>())
        {
            switch (flag.Faction)
            {
                case "legion":
                    legion++;
                    break;
                case "insurgency":
                    insurgency++;
                    break;
            }
        }

        int nocturnAnimals = 0;
        int nocturnHumans = 0;
        int nocturnAnimalsMost = 0;
        string nocturnAnimalsMostName = nobody;
        int nocturnHumansMost = 0;
        string nocturnHumansMostName = nobody;

        var nocturns = EntityManager.AllEntities<NocturnComponent>();
        if (nocturns.Count() > 0)
        {
            nocturnAnimalsMost = nocturns.MaxBy(x => x.Comp.DrinkAnimals).Comp.DrinkAnimals;
            nocturnAnimalsMostName = Name(nocturns.MaxBy(x => x.Comp.DrinkAnimals).Owner);

            nocturnHumansMost = nocturns.MaxBy(x => x.Comp.DrinkHumans).Comp.DrinkHumans;
            nocturnHumansMostName = Name(nocturns.MaxBy(x => x.Comp.DrinkHumans).Owner);

            nocturnAnimals += nocturns.Select(x => x.Comp.DrinkAnimals).Sum();
            nocturnHumans += nocturns.Select(x => x.Comp.DrinkHumans).Sum();
        }


        int nocturnTotal = nocturnAnimals + nocturnHumans;

        int traps = component.SpikeTrapActiveted;
        int humansHurt = component.HumanHurt;
        int deaths = component.HumanDeath;
        string firstDeath = component.FirstDeath;
        int openedDungeons = component.OpenedDungeons;
        string firstDungeonVisiter = component.FirstDungeonVisiter;
        int screams = component.Screams;
        double zveresHeat = Math.Round(component.ZveresHeat, 2);
        int alcohol = component.AlcoholDrink;
        int ghostBoo = component.GhostBoo;
        int ghostBooPlayers = component.GhostBooPlayers;
        int potionsTotal = component.TotalPotions;
        int lockpicksTotal = component.TotalLockpicks;
        int craftsTotal = component.TotalCrafts;
        int diggsTotal = component.TotalDiggs;
        var trapsdamage = traps * 23;

        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-war"));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-legioncontrol", ("amount", $"{legion}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-insurgencycontrol", ("amount", $"{insurgency}")));
        args.AddLine(" ");
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-research"));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-researchdungeons", ("amount", $"{openedDungeons}"), ("name", $"{firstDungeonVisiter}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-researchtraps", ("amount", $"{traps}"), ("damage", $"{trapsdamage}")));
        args.AddLine(" ");
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-fight"));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-fightdeaths", ("amount", $"{deaths}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-fightfirstdeath", ("name", $"{firstDeath}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-fighthumanshurt", ("amount", $"{humansHurt}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-fightpunchingbag", ("amount", $"{punchingBagHits}"), ("name", $"{punchingBagName}")));
        args.AddLine(" ");
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-fun"));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-funscream", ("amount", $"{screams}"), ("name", $"{screamerName}"), ("amount2", $"{screamerScreams}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-funzveres", ("amount", $"{zveresHeat}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-funalcohol", ("amount", $"{alcohol}"), ("name", $"{alcoholickName}"), ("amount2", $"{alcoholDrinks}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-funghostboo", ("amount", $"{ghostBoo}"), ("amount2", $"{ghostBooPlayers}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-funworstsmell", ("name", $"{worstSmell}"), ("name2", $"{bestSmell}")));
        args.AddLine(" ");
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-craft"));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-craftpotions", ("amount", $"{screams}"), ("name", $"{screamerName}"), ("amount2", $"{screamerScreams}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-craftlockpick", ("amount", $"{lockpicksTotal}"), ("name", $"{lockpickerName}"), ("amount2", $"{lockpickCount}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-craftcraft", ("amount", $"{craftsTotal}"), ("name", $"{crafterName}"), ("amount2", $"{craftsCount}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-craftpickaxe", ("amount", $"{diggsTotal}"), ("name", $"{diggerName}"), ("amount2", $"{diggsCount}")));
        args.AddLine(" ");
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-nocturns"));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-nocturnsblood", ("amount", $"{nocturnTotal}"), ("amount2", $"{nocturnAnimals}"), ("amount3", $"{nocturnHumans}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-nocturnshuman", ("amount", $"{nocturnHumansMost}"), ("name", $"{nocturnHumansMostName}")));
        args.AddLine(Loc.GetString("medieval-hm-barrierpoint-nocturnsanimal", ("amount", $"{nocturnAnimalsMost}"), ("name", $"{nocturnAnimalsMostName}")));
    }
}
