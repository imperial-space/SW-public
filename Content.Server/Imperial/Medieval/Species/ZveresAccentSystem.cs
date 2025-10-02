using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class ZveresAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZveresAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ZveresAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;
        message = Regex.Replace(
            message,
            Loc.GetString("medieval-hm-zveresaccent-r"),
            _random.Pick(new List<string>() { Loc.GetString("medieval-hm-zveresaccent-rr"), Loc.GetString("medieval-hm-zveresaccent-rrr") })
        );
        message = Regex.Replace(
            message,
            Loc.GetString("medieval-hm-zveresaccent-r1"),
            _random.Pick(new List<string>() { Loc.GetString("medieval-hm-zveresaccent-rr1"), Loc.GetString("medieval-hm-zveresaccent-rrr1") })
        );
        args.Message = message;
    }
}
